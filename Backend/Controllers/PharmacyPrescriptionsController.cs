
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

// Dispensing quantities are chosen by the pharmacist, constrained to
// 0..PrescribedQuantity for every medicine, and limited by branch stock.
namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy/prescriptions")]
[Authorize(Roles = "Pharmacist")]
public class PharmacyPrescriptionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PharmacyPrescriptionsController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET LOGGED-IN PHARMACIST
    // =====================================================

    private async Task<User?> GetPharmacistAsync()
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdValue, out int userId))
            return null;

        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                x.Role == "Pharmacist" &&
                x.IsActive);
    }

    // =====================================================
    // GET PENDING PRESCRIPTIONS
    // GET /api/pharmacy/prescriptions/pending
    // =====================================================

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var pharmacist = await GetPharmacistAsync();

        if (pharmacist == null ||
            !pharmacist.HospitalId.HasValue ||
            !pharmacist.BranchId.HasValue)
        {
            return Forbid();
        }

        int hospitalId = pharmacist.HospitalId.Value;
        int branchId = pharmacist.BranchId.Value;

        var prescriptions = await _context.Prescriptions
            .AsNoTracking()
            .Where(p =>
                p.HospitalId == hospitalId &&
                p.Appointment.BranchId == branchId &&
                p.Status != null && p.Status.Trim().ToLower() == "completed" &&
                !_context.PharmacyDispenses.Any(d =>
                    d.PrescriptionId == p.Id))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                prescriptionId = p.Id,
                hospitalId,
                branchId,
                p.AppointmentId,
                p.PatientId,
                patientName = p.Patient != null
                    ? p.Patient.Name
                    : "",
                doctorId = p.Appointment.DoctorId,
                doctorName = p.Appointment.Doctor != null
                    ? p.Appointment.Doctor.Name
                    : null,
                p.Diagnosis,
                p.CreatedAt,
                status = "Pending",
                medicineCount = p.Medicines.Count
            })
            .ToListAsync();

        return Ok(new
        {
            totalPending = prescriptions.Count,
            prescriptions,
            note = "Only completed doctor prescriptions without a dispensing record for the pharmacist's assigned branch are shown. Use GET status-summary to diagnose missing prescriptions."
        });
    }


    // =====================================================
    // BRANCH-SCOPED PRESCRIPTION STATUS SUMMARY
    // GET /api/pharmacy/prescriptions/status-summary
    // Counts are diagnostic; only completed, undispensed
    // prescriptions are eligible for the pending queue.
    // =====================================================
    [HttpGet("status-summary")]
    public async Task<IActionResult> GetStatusSummary()
    {
        var pharmacist = await GetPharmacistAsync();
        if (pharmacist == null || !pharmacist.HospitalId.HasValue ||
            !pharmacist.BranchId.HasValue)
            return Forbid();

        int hospitalId = pharmacist.HospitalId.Value;
        int branchId = pharmacist.BranchId.Value;

        var scoped = _context.Prescriptions.AsNoTracking()
            .Where(p => p.HospitalId == hospitalId &&
                        p.Appointment.BranchId == branchId);

        int totalPrescriptions = await scoped.CountAsync();
        int alreadyDispensed = await scoped.CountAsync(p =>
            _context.PharmacyDispenses.Any(d => d.PrescriptionId == p.Id));
        int pendingDispensing = await scoped.CountAsync(p =>
            p.Status != null && p.Status.Trim().ToLower() == "completed" &&
            !_context.PharmacyDispenses.Any(d => d.PrescriptionId == p.Id));
        int notReadyForDispensing = totalPrescriptions - alreadyDispensed - pendingDispensing;

        return Ok(new
        {
            hospitalId,
            branchId,
            totalPrescriptions,
            pendingDispensing,
            alreadyDispensed,
            notReadyForDispensing,
            note = "Pending requires a Completed doctor prescription without an existing dispensing record."
        });
    }

    // =====================================================
    // GET PRESCRIPTION DETAILS AND STOCK
    // GET /api/pharmacy/prescriptions/{id}
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var pharmacist = await GetPharmacistAsync();

        if (pharmacist == null ||
            !pharmacist.HospitalId.HasValue ||
            !pharmacist.BranchId.HasValue)
        {
            return Forbid();
        }

        int hospitalId = pharmacist.HospitalId.Value;
        int branchId = pharmacist.BranchId.Value;

        var prescription = await _context.Prescriptions
            .AsNoTracking()
            .Include(p => p.Patient)
            .Include(p => p.Medicines)
            .Include(p => p.Appointment)
                .ThenInclude(a => a.Doctor)
            .FirstOrDefaultAsync(p =>
                p.Id == id &&
                p.HospitalId == hospitalId &&
                p.Appointment.BranchId == branchId);

        if (prescription == null)
            return NotFound("Prescription not found.");

        bool dispensed = await _context.PharmacyDispenses
            .AnyAsync(x => x.PrescriptionId == id);

        var medicineDetails = new List<object>();
        bool allAvailable = prescription.Medicines.Any();
        foreach (var item in prescription.Medicines)
        {
            int availableStock = 0;
            bool medicineAvailable = false;

            if (item.MedicineId.HasValue &&
                item.PrescribedQuantity > 0)
            {
                availableStock = await _context
                    .PharmacyStockBatches
                    .Where(x =>
                        x.HospitalId == hospitalId &&
                        x.BranchId == branchId &&
                        x.MedicineId == item.MedicineId.Value &&
                        x.ExpiryDate >= DateTime.UtcNow.Date)
                    .SumAsync(x => (int?)x.Quantity) ?? 0;

                medicineAvailable =
                    availableStock >= item.PrescribedQuantity;
            }

            if (!medicineAvailable)
                allAvailable = false;

            medicineDetails.Add(new
            {
                prescriptionItemId = item.Id,
                item.MedicineId,
                item.MedicineName,
                item.Dosage,
                item.Frequency,
                item.Duration,
                item.Notes,
                item.PrescribedQuantity,
                maximumPrescriptionQuantity = item.PrescribedQuantity,
                canEnterHigherQuantityIfOtc = false,
                canReduceQuantity = true,
                canRequestAdditionalOtcQuantity = false,
                additionalQuantityRequiresSeparateOtcSale = false, // This dispensing endpoint never permits excess quantities.
                availableStock,
                medicineAvailable
            });
        }

        return Ok(new
        {
            prescription.Id,
            hospitalId,
            branchId,
            prescription.AppointmentId,
            prescription.PatientId,
            patientName = prescription.Patient?.Name,
            doctorId = prescription.Appointment.DoctorId,
            doctorName = prescription.Appointment.Doctor?.Name,
            prescription.Diagnosis,
            prescription.Instructions,
            prescription.CreatedAt,
            prescription.Status,
            dispensingStatus = dispensed
                ? "Dispensed"
                : "Pending",
            allMedicinesAvailable = allAvailable && !dispensed,
            medicines = medicineDetails
        });
    }

    // =====================================================
    // GET SAVED DISPENSING DETAILS FOR BILLING
    // GET /api/pharmacy/prescriptions/{id}/dispense
    // The saved quantities are authoritative for billing.
    // =====================================================

    [HttpGet("{id:int}/dispense")]
    public async Task<IActionResult> GetDispense(int id)
    {
        var pharmacist = await GetPharmacistAsync();
        if (pharmacist == null ||
            !pharmacist.HospitalId.HasValue ||
            !pharmacist.BranchId.HasValue)
            return Forbid();

        int hospitalId = pharmacist.HospitalId.Value;
        int branchId = pharmacist.BranchId.Value;

        var dispense = await _context.PharmacyDispenses
            .AsNoTracking()
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d =>
                d.PrescriptionId == id &&
                d.HospitalId == hospitalId &&
                d.BranchId == branchId);

        if (dispense == null)
            return NotFound(new { message = "No saved dispensing record was found for this prescription and branch." });

        var savedItems = dispense.Items
            .OrderBy(i => i.PrescriptionItemId)
            .ThenBy(i => i.StockBatchId)
            .Select(i => new
            {
                i.PrescriptionItemId,
                i.StockBatchId,
                i.Quantity,
                i.UnitPrice,
                lineTotal = i.Quantity * i.UnitPrice
            })
            .ToList();

        decimal totalAmount = savedItems.Sum(i => i.lineTotal);
        var prescribedItems = await _context.Prescriptions.AsNoTracking()
            .Where(p => p.Id == id && p.HospitalId == hospitalId &&
                        p.Appointment.BranchId == branchId)
            .SelectMany(p => p.Medicines)
            .Select(i => new { i.Id, i.MedicineId, i.PrescribedQuantity })
            .ToListAsync();
        var quantitySummary = prescribedItems.Select(i => new
        {
            prescriptionItemId = i.Id,
            medicineId = i.MedicineId,
            prescribedQuantity = i.PrescribedQuantity,
            dispensedQuantity = savedItems.Where(x => x.PrescriptionItemId == i.Id).Sum(x => x.Quantity),
            extraQuantity = Math.Max(0, savedItems.Where(x => x.PrescriptionItemId == i.Id).Sum(x => x.Quantity) - i.PrescribedQuantity)
        }).ToList();

        return Ok(new
        {
            dispenseId = dispense.Id,
            dispense.PrescriptionId,
            dispense.BranchId,
            dispense.DispensedAt,
            status = "Dispensed",
            totalQuantity = savedItems.Sum(i => i.Quantity),
            totalAmount,
            quantitySummary,
            items = savedItems
        });
    }

    // =====================================================
    // DISPENSE MEDICINES AND AUTOMATICALLY DEDUCT STOCK
    // POST /api/pharmacy/prescriptions/{id}/dispense
    // =====================================================

    [HttpPost("{id:int}/dispense")]
    public async Task<IActionResult> Dispense(int id, [FromBody] DispenseSelectionRequest request)
    {
        var pharmacist = await GetPharmacistAsync();

        if (pharmacist == null ||
            !pharmacist.HospitalId.HasValue ||
            !pharmacist.BranchId.HasValue)
        {
            return Forbid();
        }

        if (request?.Items == null || request.Items.Count == 0)
            return BadRequest(new { message = "Select the quantity for each prescription medicine before dispensing." });

        if (request.Items.Any(x => x.PrescriptionItemId <= 0 || x.Quantity < 0) ||
            request.Items.GroupBy(x => x.PrescriptionItemId).Any(g => g.Count() > 1))
            return BadRequest(new { message = "Provide unique valid prescriptionItemIds and non-negative quantities." });

        int hospitalId = pharmacist.HospitalId.Value;
        int branchId = pharmacist.BranchId.Value;

        // Serializable isolation protects stock reads and updates
        // from concurrent dispensing requests.
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var prescription = await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Appointment)
                .Include(p => p.Medicines)
                .FirstOrDefaultAsync(p =>
                    p.Id == id &&
                    p.HospitalId == hospitalId &&
                    p.Appointment.BranchId == branchId);

            if (prescription == null)
            {
                return NotFound(new
                {
                    message = "Prescription not found."
                });
            }

            if (!string.Equals(prescription.Status?.Trim(), "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        "Only completed prescriptions can be dispensed."
                });
            }

            bool alreadyDispensed =
                await _context.PharmacyDispenses
                    .AnyAsync(d => d.PrescriptionId == id);

            if (alreadyDispensed)
            {
                return Conflict(new
                {
                    message =
                        "This prescription has already been dispensed."
                });
            }

            bool pharmacyBillExists = await _context.Billings.AsNoTracking()
                .AnyAsync(b => b.AppointmentId == prescription.AppointmentId &&
                               b.HospitalId == hospitalId && b.BranchId == branchId &&
                               b.BillingType == "Pharmacy");
            if (pharmacyBillExists)
                return Conflict(new { message = "A pharmacy bill already exists for this appointment. Do not dispense against a finalized billing workflow." });

            var items = prescription.Medicines.ToList();

            if (items.Count == 0)
            {
                return BadRequest(new
                {
                    message =
                        "Prescription does not contain medicines."
                });
            }

            if (items.Any(x =>
                !x.MedicineId.HasValue ||
                x.PrescribedQuantity <= 0))
            {
                return BadRequest(new
                {
                    message =
                        "All prescribed medicines must have a valid MedicineId and PrescribedQuantity."
                });
            }

            // Every prescribed item has an explicit patient-selected quantity.
            // Zero means the patient declined that medicine.
            if (request.Items.Count != items.Count ||
                items.Any(x => !request.Items.Any(r => r.PrescriptionItemId == x.Id)))
                return BadRequest(new { message = "Provide one selected quantity for every prescription item (zero is allowed)." });

            // A pharmacist may dispense fewer units or the prescribed amount,
            // but never more than the doctor's prescribed quantity, for any medicine.
            // The original prescription is not modified.
            foreach (var item in items)
            {
                int selected = request.Items.Single(x => x.PrescriptionItemId == item.Id).Quantity;
                if (selected < 0 || selected > item.PrescribedQuantity)
                    return BadRequest(new
                    {
                        message = "Dispensed quantity must be between 0 and the prescribed quantity.",
                        prescriptionItemId = item.Id,
                        medicineId = item.MedicineId,
                        prescribedQuantity = item.PrescribedQuantity,
                        requestedQuantity = selected
                    });
            }

            if (request.Items.All(x => x.Quantity == 0))
                return BadRequest(new { message = "Select at least one medicine with a quantity greater than zero." });

            // Group the pharmacist's selected quantities, not the doctor's defaults.
            var requiredQuantities = items
                .GroupBy(x => x.MedicineId!.Value)
                .Select(g => new
                {
                    MedicineId = g.Key,
                    Quantity = g.Sum(x => (long)request.Items.Single(r => r.PrescriptionItemId == x.Id).Quantity)
                })
                .ToList();

            var medicineIds = requiredQuantities
                .Select(x => x.MedicineId)
                .ToList();

            // Verify medicines belong to this clinic.
            var validMedicineIds = await _context.Medicines
                .AsNoTracking()
                .Where(m =>
                    m.HospitalId == hospitalId &&
                    m.IsActive &&
                    medicineIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync();

            if (validMedicineIds.Count != medicineIds.Count)
            {
                return BadRequest(new
                {
                    message =
                        "One or more medicines are inactive or do not belong to this clinic."
                });
            }

            DateTime now = DateTime.UtcNow;

            var batches = await _context.PharmacyStockBatches
                .Where(b =>
                    b.HospitalId == hospitalId &&
                    b.BranchId == branchId &&
                    medicineIds.Contains(b.MedicineId) &&
                    b.Quantity > 0 &&
                    b.ExpiryDate >= now.Date)
                .OrderBy(b => b.ExpiryDate)
                .ThenBy(b => b.Id)
                .ToListAsync();

            // Check ALL medicines before deducting stock.
            foreach (var required in requiredQuantities)
            {
                long available = batches
                    .Where(b => b.MedicineId == required.MedicineId)
                    .Sum(b => (long)b.Quantity);

                if (available < required.Quantity)
                {
                    return BadRequest(new
                    {
                        message = "Insufficient medicine stock.",
                        medicineId = required.MedicineId,
                        requiredQuantity = required.Quantity,
                        availableQuantity = available
                    });
                }
            }

            var dispense = new PharmacyDispense
            {
                HospitalId = hospitalId,
                BranchId = branchId,
                PrescriptionId = prescription.Id,
                PharmacistUserId = pharmacist.Id,
                DispensedAt = now
            };

            decimal totalAmount = 0m;

            // Allocate from earliest-expiring batches first.
            foreach (var item in items)
            {
                int remaining = request.Items.Single(x => x.PrescriptionItemId == item.Id).Quantity;

                foreach (var batch in batches
                    .Where(b =>
                        b.MedicineId == item.MedicineId!.Value &&
                        b.Quantity > 0))
                {
                    if (remaining == 0)
                        break;

                    int allocated = Math.Min(
                        remaining,
                        batch.Quantity);

                    if (allocated <= 0)
                        continue;

                    batch.Quantity -= allocated;
                    remaining -= allocated;

                    dispense.Items.Add(new PharmacyDispenseItem
                    {
                        PrescriptionItemId = item.Id,
                        StockBatchId = batch.Id,
                        Quantity = allocated,
                        UnitPrice = batch.SellingPrice
                    });

                    totalAmount +=
                        allocated * batch.SellingPrice;
                }

                if (remaining != 0)
                {
                    throw new InvalidOperationException(
                        "Stock allocation failed.");
                }
            }

            _context.PharmacyDispenses.Add(dispense);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message =
                    "Medicines dispensed successfully.",
                prescriptionId = prescription.Id,
                dispenseId = dispense.Id,
                status = "Dispensed",
                totalAmount,
                totalQuantity = dispense.Items.Sum(x => x.Quantity),
                quantitySource = "Saved pharmacist dispensing allocations",
                quantitySummary = items.Select(item => new
                {
                    prescriptionItemId = item.Id,
                    medicineId = item.MedicineId,
                    prescribedQuantity = item.PrescribedQuantity,
                    dispensedQuantity = dispense.Items.Where(x => x.PrescriptionItemId == item.Id).Sum(x => x.Quantity),
                    extraQuantity = Math.Max(0, dispense.Items.Where(x => x.PrescriptionItemId == item.Id).Sum(x => x.Quantity) - item.PrescribedQuantity)
                }).ToList(),
                hospitalId,
                branchId,
                items = dispense.Items.Select(x => new
                {
                    x.PrescriptionItemId,
                    x.StockBatchId,
                    x.Quantity,
                    x.UnitPrice,
                    lineTotal = x.Quantity * x.UnitPrice
                })
            });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            return Conflict(new
            {
                message =
                    "Dispensing could not be completed because the stock or prescription was updated. Please refresh and retry."
            });
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync();

            return Conflict(new
            {
                message =
                    "Stock allocation failed. Please refresh the prescription and retry."
            });
        }
    }

    // =====================================================
    // UPDATE SAVED DISPENSING QUANTITIES BEFORE BILLING
    // PUT /api/pharmacy/prescriptions/{id}/dispense/quantities
    // Excess prescription-only units require additional authorization;
    // verified OTC medicines have no arbitrary extra-unit cap.
    // =====================================================
    [HttpPut("{id:int}/dispense/quantities")]
    public async Task<IActionResult> UpdateDispensedQuantities(
        int id, [FromBody] UpdateDispensedQuantitiesRequest request)
    {
        var pharmacist = await GetPharmacistAsync();
        if (pharmacist == null || !pharmacist.HospitalId.HasValue ||
            !pharmacist.BranchId.HasValue)
            return Forbid();

        if (request?.Items == null || request.Items.Count == 0)
            return BadRequest(new { message = "At least one dispensing item is required." });

        if (request.Items.Any(x => x.PrescriptionItemId <= 0 || x.Quantity < 0) ||
            request.Items.GroupBy(x => x.PrescriptionItemId).Any(g => g.Count() > 1))
            return BadRequest(new { message = "Items must have unique valid prescriptionItemIds and non-negative quantities." });

        int hospitalId = pharmacist.HospitalId.Value;
        int branchId = pharmacist.BranchId.Value;

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);
        try
        {
            var prescription = await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Medicines)
                .FirstOrDefaultAsync(p => p.Id == id && p.HospitalId == hospitalId &&
                    p.Appointment.BranchId == branchId && p.Status != null &&
                    p.Status.Trim().ToLower() == "completed");
            if (prescription == null)
                return NotFound(new { message = "Completed prescription not found for this branch." });

            var dispense = await _context.PharmacyDispenses
                .Include(d => d.Items)
                .FirstOrDefaultAsync(d => d.PrescriptionId == id &&
                    d.HospitalId == hospitalId && d.BranchId == branchId);
            if (dispense == null)
                return NotFound(new { message = "Dispense medicines first before adjusting quantities." });

            // An invoice must not be silently altered after it is generated.
            bool billExists = await _context.Billings.AsNoTracking().AnyAsync(b =>
                b.AppointmentId == prescription.AppointmentId &&
                b.HospitalId == hospitalId && b.BranchId == branchId &&
                b.BillingType == "Pharmacy");
            if (billExists)
                return Conflict(new { message = "A pharmacy bill already exists. Dispensing cannot be changed after bill generation; use an authorized cancellation/refund and new billing workflow." });

            var prescribed = prescription.Medicines.ToList();
            if (prescribed.Count != request.Items.Count ||
                prescribed.Any(p => !request.Items.Any(x => x.PrescriptionItemId == p.Id)))
                return BadRequest(new { message = "Provide exactly one quantity for every prescription item." });

            foreach (var item in prescribed)
            {
                int quantity = request.Items.Single(x => x.PrescriptionItemId == item.Id).Quantity;
                if (!item.MedicineId.HasValue || item.PrescribedQuantity <= 0)
                    return BadRequest(new
                    {
                        message = "A prescription item has an invalid medicine or prescribed quantity.",
                        prescriptionItemId = item.Id
                    });

                if (quantity < 0 || quantity > item.PrescribedQuantity)
                    return BadRequest(new
                    {
                        message = "Dispensed quantity must be between 0 and the prescribed quantity.",
                        prescriptionItemId = item.Id,
                        medicineId = item.MedicineId,
                        prescribedQuantity = item.PrescribedQuantity,
                        requestedQuantity = quantity
                    });
            }

            if (request.Items.All(x => x.Quantity == 0))
                return BadRequest(new { message = "Select at least one medicine with a quantity greater than zero." });

            // If the requested quantities are already saved, do not delete and recreate
            // dispensing rows. Reallocation can change stock batches/prices even when
            // the pharmacist entered exactly the same quantities, causing billing
            // snapshots to appear stale and triggering avoidable 409 responses.
            bool quantitiesUnchanged = prescribed.All(item =>
                dispense.Items.Where(x => x.PrescriptionItemId == item.Id)
                    .Sum(x => x.Quantity) ==
                request.Items.Single(x => x.PrescriptionItemId == item.Id).Quantity);

            if (quantitiesUnchanged)
            {
                var existingItems = dispense.Items
                    .OrderBy(x => x.PrescriptionItemId)
                    .ThenBy(x => x.StockBatchId)
                    .ThenBy(x => x.Id)
                    .Select(x => new
                    {
                        x.PrescriptionItemId,
                        x.StockBatchId,
                        x.Quantity,
                        x.UnitPrice,
                        lineTotal = x.Quantity * x.UnitPrice
                    }).ToList();

                return Ok(new
                {
                    message = "Dispensed quantities are already saved. No stock changes were made; you can generate the bill.",
                    prescriptionId = id,
                    dispenseId = dispense.Id,
                    branchId,
                    unchanged = true,
                    totalQuantity = existingItems.Sum(x => x.Quantity),
                    totalAmount = existingItems.Sum(x => x.lineTotal),
                    quantitySummary = prescribed.Select(item => new
                    {
                        prescriptionItemId = item.Id,
                        medicineId = item.MedicineId,
                        prescribedQuantity = item.PrescribedQuantity,
                        dispensedQuantity = existingItems.Where(x => x.PrescriptionItemId == item.Id).Sum(x => x.Quantity),
                        extraQuantity = Math.Max(0, existingItems.Where(x => x.PrescriptionItemId == item.Id).Sum(x => x.Quantity) - item.PrescribedQuantity)
                    }).ToList(),
                    items = existingItems
                });
            }

            DateTime now = DateTime.UtcNow;
            var medicineIds = prescribed.Select(x => x.MedicineId!.Value).Distinct().ToList();
            var batchIds = dispense.Items.Select(x => x.StockBatchId).Distinct().ToList();
            var batches = await _context.PharmacyStockBatches
                .Where(b => b.HospitalId == hospitalId && b.BranchId == branchId &&
                    (medicineIds.Contains(b.MedicineId) || batchIds.Contains(b.Id)))
                .OrderBy(b => b.ExpiryDate).ThenBy(b => b.Id)
                .ToListAsync();

            // Restore the original allocation in memory before recalculating.
            // The whole operation commits atomically or rolls back.
            foreach (var oldItem in dispense.Items)
            {
                var oldBatch = batches.FirstOrDefault(b => b.Id == oldItem.StockBatchId);
                if (oldBatch == null)
                    return Conflict(new { message = "Original stock batch is missing. Contact an administrator." });
                oldBatch.Quantity = checked(oldBatch.Quantity + oldItem.Quantity);
            }

            var newItems = new List<PharmacyDispenseItem>();
            foreach (var item in prescribed)
            {
                int remaining = request.Items.Single(x => x.PrescriptionItemId == item.Id).Quantity;
                foreach (var batch in batches.Where(b =>
                    b.MedicineId == item.MedicineId!.Value &&
                    b.ExpiryDate >= now.Date && b.Quantity > 0))
                {
                    if (remaining == 0) break;
                    int allocated = Math.Min(remaining, batch.Quantity);
                    batch.Quantity -= allocated;
                    remaining -= allocated;
                    newItems.Add(new PharmacyDispenseItem
                    {
                        PrescriptionItemId = item.Id,
                        StockBatchId = batch.Id,
                        Quantity = allocated,
                        UnitPrice = batch.SellingPrice
                    });
                }
                if (remaining > 0)
                    return BadRequest(new
                    {
                        message = "Insufficient unexpired stock for requested dispensing quantity.",
                        prescriptionItemId = item.Id,
                        shortage = remaining
                    });
            }

            _context.PharmacyDispenseItems.RemoveRange(dispense.Items);
            dispense.Items.Clear();
            foreach (var newItem in newItems)
                dispense.Items.Add(newItem);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Dispensed quantities and stock updated successfully. Generate the bill using these saved quantities.",
                prescriptionId = id,
                dispenseId = dispense.Id,
                branchId,
                totalQuantity = newItems.Sum(x => x.Quantity),
                totalAmount = newItems.Sum(x => x.Quantity * x.UnitPrice),
                quantitySummary = prescribed.Select(item => new
                {
                    prescriptionItemId = item.Id,
                    medicineId = item.MedicineId,
                    prescribedQuantity = item.PrescribedQuantity,
                    dispensedQuantity = newItems.Where(x => x.PrescriptionItemId == item.Id).Sum(x => x.Quantity),
                    extraQuantity = Math.Max(0, newItems.Where(x => x.PrescriptionItemId == item.Id).Sum(x => x.Quantity) - item.PrescribedQuantity)
                }).ToList(),
                items = newItems.Select(x => new {
                    x.PrescriptionItemId,
                    x.StockBatchId,
                    x.Quantity,
                    x.UnitPrice,
                    lineTotal = x.Quantity * x.UnitPrice
                }).ToList()
            });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Conflict(new { message = "Stock was modified concurrently. Refresh and retry." });
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync();
            return Conflict(new { message = "Stock quantity overflow. Contact an administrator." });
        }
    }

    // Pharmacist selects quantities BEFORE stock is deducted.
    // Prescription dispensing is limited to the doctor's prescribed quantity.
    // Additional OTC items must be processed through the separate OTC sale flow.
    public sealed class DispenseSelectionRequest
    {
        public List<DispensedQuantityItem> Items { get; set; } = new();
    }

    public sealed class UpdateDispensedQuantitiesRequest
    {
        public List<DispensedQuantityItem> Items { get; set; } = new();
    }

    public sealed class DispensedQuantityItem
    {
        public int PrescriptionItemId { get; set; }
        public int Quantity { get; set; }
    }

}
