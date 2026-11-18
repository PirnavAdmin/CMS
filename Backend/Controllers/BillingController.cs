using AuthDemo.Authorization;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[RequirePermission("Billing")]
[Route("api/[controller]")]
[Authorize(Roles = "Receptionist,Admin,Pharmacist")]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly ILogger<BillingController> _logger;
    private readonly IConfiguration _billingTaxConfiguration;

    public BillingController(
        AppDbContext context,
        NotificationService notificationService,
        ILogger<BillingController> logger,
        IConfiguration billingTaxConfiguration)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
        _billingTaxConfiguration = billingTaxConfiguration;
    }

    // Resolve active Admin recipients from the Users table for this hospital.
    // Never broadcast pharmacy bills to every user with the Admin role.
    private async Task NotifyPharmacyAdminAboutBillAsync(Billing bill)
    {
        if (!string.Equals(bill.BillingType, "Pharmacy", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var adminUserIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.HospitalId == bill.HospitalId &&
                            u.Role == "Admin" && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();

            if (adminUserIds.Count == 0)
            {
                _logger.LogWarning(
                    "Pharmacy bill {BillId} saved, but no active Admin found for HospitalId {HospitalId}.",
                    bill.Id, bill.HospitalId);
                return;
            }

            foreach (var adminUserId in adminUserIds)
            {
                try
                {
                    await _notificationService.SendPharmacyAdminNotificationAsync(
                        adminUserId,
                        "Pharmacy Bill Generated",
                        $"Pharmacy bill {bill.InvoiceNumber ?? $"BILL-{bill.Id}"} has been generated for Branch {bill.BranchId}. Total amount: ₹{bill.TotalAmount:F2}.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to notify Admin {AdminUserId} for pharmacy bill {BillId}.",
                        adminUserId, bill.Id);
                }
            }
        }
        catch (Exception ex)
        {
            // The bill is already saved; do not fail the request and invite a duplicate retry.
            _logger.LogError(ex,
                "Failed to resolve Admin recipients for pharmacy bill {BillId}.", bill.Id);
        }
    }

    // Step 4: Save invoice tax totals and a separately identified tax split.
    // TaxType is supplied explicitly; do not infer intra/inter-state supply from GST rate.
    // A bill-level rate is suitable only when all billed items share the same treatment.
    // Rates are administered on the server. Do not trust a browser-supplied GST rate.
    // A single rate per bill type is only suitable for invoices where all items
    // have the same verified tax treatment. For mixed-rate pharmacy invoices,
    // introduce persisted line-level tax snapshots before enabling GST.
    private decimal GetConfiguredGstRate(string billingType)
    {
        var value = _billingTaxConfiguration[$"BillingTax:{billingType}:GstPercentage"];
        if (string.IsNullOrWhiteSpace(value)) return 0m; // fail closed; no invented rate
        if (!decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var rate)
            || rate < 0m || rate > 100m)
            throw new InvalidOperationException($"Invalid BillingTax:{billingType}:GstPercentage configuration.");
        return rate;
    }

    private string GetConfiguredTaxType(string billingType, decimal rate)
    {
        if (rate == 0m) return "None";
        var taxType = NormalizeTaxType(_billingTaxConfiguration[$"BillingTax:{billingType}:TaxType"]);
        if (taxType is not ("CGST_SGST" or "IGST"))
            throw new InvalidOperationException($"Configure BillingTax:{billingType}:TaxType as CGST_SGST or IGST.");
        return taxType;
    }

    private static void ApplyTaxBreakdown(Billing bill, string taxType)
    {
        bill.DiscountAmount = Math.Round(bill.SubTotal * bill.Discount / 100m, 2);
        bill.TaxableAmount = bill.SubTotal - bill.DiscountAmount;
        bill.GstAmount = Math.Round(bill.TaxableAmount * bill.GstPercentage / 100m, 2);
        bill.TotalAmount = bill.TaxableAmount + bill.GstAmount;

        bill.CgstPercentage = 0m;
        bill.SgstPercentage = 0m;
        bill.IgstPercentage = 0m;
        bill.CgstAmount = 0m;
        bill.SgstAmount = 0m;
        bill.IgstAmount = 0m;

        if (bill.GstPercentage == 0m)
        {
            bill.TaxType = "None";
        }
        else if (taxType == "CGST_SGST")
        {
            bill.TaxType = "CGST_SGST";
            bill.CgstPercentage = bill.GstPercentage / 2m;
            bill.SgstPercentage = bill.GstPercentage - bill.CgstPercentage;
            bill.CgstAmount = Math.Round(bill.GstAmount / 2m, 2);
            bill.SgstAmount = bill.GstAmount - bill.CgstAmount;
        }
        else if (taxType == "IGST")
        {
            bill.TaxType = "IGST";
            bill.IgstPercentage = bill.GstPercentage;
            bill.IgstAmount = bill.GstAmount;
        }
        else
        {
            // Preserve total GST without claiming a specific jurisdictional split.
            bill.TaxType = "Unspecified";
        }
    }

    private static string? NormalizeTaxType(string? taxType)
    {
        if (string.IsNullOrWhiteSpace(taxType)) return "Unspecified";
        var value = taxType.Trim().ToUpperInvariant().Replace("-", "_");
        return value switch
        {
            "CGST_SGST" => "CGST_SGST",
            "IGST" => "IGST",
            "UNSPECIFIED" => "Unspecified",
            "NONE" => "None",
            _ => null
        };
    }

    private int HospitalId =>
        int.TryParse(
            User.FindFirstValue("HospitalId"),
            out var id)
            ? id
            : 0;

    private int? BranchId =>
        int.TryParse(
            User.FindFirstValue("BranchId"),
            out var id)
            ? id
            : null;

    private IActionResult? RestrictPharmacistToPharmacy(string? billingType)
    {
        if (User.IsInRole("Pharmacist") &&
            !string.Equals(billingType, "Pharmacy", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, new { message = "Pharmacists may access Pharmacy billing only." });
        return null;
    }

    // Medicine charges are calculated exclusively from saved dispensing items.
    // Return null when no dispensing record exists; SumAsync alone returns zero
    // for an empty set and cannot distinguish missing dispensing from zero cost.
    private async Task<decimal?> GetDispensedMedicineTotalAsync(
        int appointmentId, int hospitalId, int? branchId)
    {
        if (!branchId.HasValue)
            return null;

        var lines =
            from prescription in _context.Prescriptions.AsNoTracking()
            join dispense in _context.PharmacyDispenses.AsNoTracking()
                on prescription.Id equals dispense.PrescriptionId
            join item in _context.PharmacyDispenseItems.AsNoTracking()
                on dispense.Id equals item.PharmacyDispenseId
            join prescribed in _context.PrescriptionItems.AsNoTracking()
                on item.PrescriptionItemId equals prescribed.Id
            where prescription.AppointmentId == appointmentId
                && prescription.HospitalId == hospitalId
                && prescription.Appointment.BranchId == branchId.Value
                && dispense.HospitalId == hospitalId
                && dispense.BranchId == branchId.Value
                && prescribed.PrescriptionId == prescription.Id
                && item.Quantity > 0
            select new { item.Quantity, item.UnitPrice };

        if (!await lines.AnyAsync())
            return null;

        return await lines.SumAsync(x => (decimal?)(x.Quantity * x.UnitPrice));
    }

    // Returns the exact saved dispense lines used to calculate pharmacy charges.
    // This is a live view of dispensing records, not a historical invoice snapshot.
    private async Task<List<PharmacyBillingLine>> GetPharmacyBillingLinesAsync(
        int appointmentId, int hospitalId, int branchId)
    {
        return await (
            from prescription in _context.Prescriptions.AsNoTracking()
            join dispense in _context.PharmacyDispenses.AsNoTracking()
                on prescription.Id equals dispense.PrescriptionId
            join item in _context.PharmacyDispenseItems.AsNoTracking()
                on dispense.Id equals item.PharmacyDispenseId
            join prescribed in _context.PrescriptionItems.AsNoTracking()
                on item.PrescriptionItemId equals prescribed.Id
            where prescription.AppointmentId == appointmentId
                && prescription.HospitalId == hospitalId
                && prescription.Appointment.BranchId == branchId
                && dispense.HospitalId == hospitalId
                && dispense.BranchId == branchId
                && prescribed.PrescriptionId == prescription.Id
                && item.Quantity > 0
            orderby dispense.DispensedAt, item.Id
            select new PharmacyBillingLine
            {
                PrescriptionId = prescription.Id,
                PrescriptionItemId = prescribed.Id,
                MedicineName = prescribed.MedicineName,
                PrescribedQuantity = prescribed.PrescribedQuantity,
                StockBatchId = item.StockBatchId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }
        ).ToListAsync();
    }

    private sealed class PharmacyBillingLine
    {
        public int PrescriptionId { get; set; }
        public int PrescriptionItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int PrescribedQuantity { get; set; }
        public int StockBatchId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
    }

    // Aggregate per prescription item, not per stock batch. An item may span
    // multiple batches; prescribed quantity must never be counted repeatedly.
    private static object GetDispensingQuantitySummary(IEnumerable<PharmacyBillingLine> lines)
    {
        return lines.GroupBy(x => new { x.PrescriptionItemId, x.MedicineName, x.PrescribedQuantity })
            .Select(g => new
            {
                prescriptionItemId = g.Key.PrescriptionItemId,
                medicineName = g.Key.MedicineName,
                prescribedQuantity = g.Key.PrescribedQuantity,
                dispensedQuantity = g.Sum(x => x.Quantity),
                extraQuantity = Math.Max(0, g.Sum(x => x.Quantity) - g.Key.PrescribedQuantity),
                additionalQuantity = Math.Max(0, g.Sum(x => x.Quantity) - g.Key.PrescribedQuantity),
                remainingUndispensedQuantity = Math.Max(0, g.Key.PrescribedQuantity - g.Sum(x => x.Quantity)),
                lineTotal = g.Sum(x => x.LineTotal)
            }).ToList();
    }

    // Enforce the pharmacy rule for each prescription item, even when a
    // prescription item was allocated across multiple stock batches.
    // A reduced quantity is valid; an increase is never billable.
    private static object? ValidatePrescriptionDispensingLimits(
        IEnumerable<PharmacyBillingLine> lines)
    {
        foreach (var group in lines.GroupBy(x => new
        {
            x.PrescriptionItemId,
            x.MedicineName,
            x.PrescribedQuantity
        }))
        {
            long dispensedQuantity = group.Sum(x => (long)x.Quantity);
            if (dispensedQuantity < 0 ||
                dispensedQuantity > group.Key.PrescribedQuantity)
            {
                return new
                {
                    message = "Dispensed quantity cannot exceed the doctor's prescribed quantity.",
                    prescriptionItemId = group.Key.PrescriptionItemId,
                    medicineName = group.Key.MedicineName,
                    prescribedQuantity = group.Key.PrescribedQuantity,
                    dispensedQuantity
                };
            }
        }
        return null;
    }

    // OTC items are stock-deducted when the OTC sale is committed, not during billing.
    private async Task<List<OtcBillingLine>> GetOtcLinesAsync(
        int appointmentId, int hospitalId, int branchId, bool includeCommitted = true)
    {
        var query = from sale in _context.PharmacyOtcSales.AsNoTracking()
                    join item in _context.PharmacyOtcSaleItems.AsNoTracking()
                        on sale.Id equals item.PharmacyOtcSaleId
                    join medicine in _context.Medicines.AsNoTracking()
                        on item.MedicineId equals medicine.Id
                    where sale.AppointmentId == appointmentId
                        && sale.HospitalId == hospitalId
                        && sale.BranchId == branchId
                        && medicine.HospitalId == hospitalId
                        && (sale.Status == "Billed" ||
                            (includeCommitted && sale.Status == "Committed"))
                    select new OtcBillingLine
                    {
                        OtcSaleId = sale.Id,
                        MedicineId = item.MedicineId,
                        MedicineName = medicine.Name,
                        StockBatchId = item.StockBatchId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        GstPercentage = item.GstPercentage
                    };
        return await query.ToListAsync();
    }

    private sealed class OtcBillingLine
    {
        public int OtcSaleId { get; set; }
        public int MedicineId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int StockBatchId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GstPercentage { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
    }

    private static string? NormalizeBillingType(string? billingType)
    {
        if (string.IsNullOrWhiteSpace(billingType))
        {
            return null;
        }

        var allowedTypes = new[]
        {
            "OP",
            "Lab",
            "Pharmacy"
        };

        return allowedTypes.FirstOrDefault(x =>
            x.Equals(
                billingType.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    private IActionResult? ValidateScope()
    {
        if (HospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token."
            });
        }

        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")) &&
            !BranchId.HasValue)
        {
            return Forbid();
        }

        return null;
    }

    // =====================================================
    // GET APPOINTMENTS AVAILABLE FOR BILLING
    // =====================================================

    [HttpGet("appointments")]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null)
    {
        var scopeError = ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var query = _context.Appointments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Branch)
            .Where(x =>
                x.HospitalId == HospitalId);

        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")))
        {
            query = query.Where(x =>
                x.BranchId == BranchId!.Value);
        }

        if (date.HasValue)
        {
            var selectedDate = date.Value.Date;

            query = query.Where(x =>
                x.Date.Date == selectedDate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                x.Patient.Name.Contains(term) ||
                x.Patient.PatientCode.Contains(term) ||
                x.Patient.Phone.Contains(term) ||
                x.Doctor.Name.Contains(term));
        }

        var appointments = await query
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.StartTime)
            .Select(x => new
            {
                appointmentId = x.Id,
                patientId = x.PatientId,
                patientName = x.Patient.Name,
                patientCode = x.Patient.PatientCode,
                patientPhone = x.Patient.Phone,

                doctorId = x.DoctorId,
                doctorName = x.Doctor.Name,

                hospitalId = x.HospitalId,
                branchId = x.BranchId,
                branchName = x.Branch != null
                    ? x.Branch.Name
                    : null,

                date = x.Date,
                startTime = x.StartTime,
                consultationFee = x.ConsultationFee > 0
                    ? x.ConsultationFee
                    : x.Doctor.Fees,
                status = x.Status,
                paymentStatus = x.PaymentStatus
            })
            .ToListAsync();

        return Ok(appointments);
    }

    // Lab billing uses only tests actually ordered for the appointment.
    // Price is taken from LabOrders (the order-time charge), not today's master price.
    private sealed class LabBillingLine
    {
        public int LabOrderId { get; set; }
        public string TestCode { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public int Quantity => 1;
        public decimal LineTotal => UnitPrice;
    }

    private async Task<List<LabBillingLine>> GetLabBillingLinesAsync(int appointmentId, int hospitalId)
    {
        return await _context.LabOrders.AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId &&
                        x.HospitalId == hospitalId && x.Status != "Cancelled")
            .OrderBy(x => x.Id)
            .Select(x => new LabBillingLine
            {
                LabOrderId = x.Id,
                TestCode = x.TestCode,
                TestName = x.TestName,
                UnitPrice = x.Price,
                Status = x.Status
            })
            .ToListAsync();
    }

    // GET /api/Billing/appointments/{appointmentId}/lab-tests
    // Use this endpoint to populate the Lab Billing screen.
    [HttpGet("appointments/{appointmentId:int}/lab-tests")]
    public async Task<IActionResult> GetAppointmentLabTests(int appointmentId)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;
        if (User.IsInRole("Pharmacist"))
            return StatusCode(403, new { message = "Pharmacists may access Pharmacy billing only." });

        var appointment = await _context.Appointments.AsNoTracking()
            .Where(x => x.Id == appointmentId && x.HospitalId == HospitalId)
            .Select(x => new { x.Id, x.PatientId, x.BranchId })
            .FirstOrDefaultAsync();
        if (appointment == null)
            return NotFound(new { message = "Appointment not found." });
        if (User.IsInRole("Receptionist") && appointment.BranchId != BranchId)
            return Forbid();

        var tests = await GetLabBillingLinesAsync(appointmentId, HospitalId);
        return Ok(new
        {
            appointmentId = appointment.Id,
            patientId = appointment.PatientId,
            branchId = appointment.BranchId,
            labTests = tests.Select(x => new
            {
                labOrderId = x.LabOrderId,
                testCode = x.TestCode,
                testName = x.TestName,
                quantity = x.Quantity,
                unitPrice = x.UnitPrice,
                lineTotal = x.LineTotal,
                status = x.Status
            }).ToList(),
            labTotal = tests.Sum(x => x.LineTotal),
            totalTests = tests.Count
        });
    }

    // =====================================================
    // GET PRESCRIBED MEDICINES FOR PHARMACY BILLING
    // GET /api/Billing/appointments/{appointmentId}/prescription
    // Receptionist is restricted to the assigned branch.
    // Prices are intentionally not invented here because PrescriptionItem
    // currently stores medicine instructions, not medicine selling prices.
    // =====================================================

    [HttpGet("appointments/{appointmentId:int}/prescription")]
    public async Task<IActionResult> GetAppointmentPrescription(int appointmentId)
    {
        var scopeError = ValidateScope();
        if (scopeError != null)
        {
            return scopeError;
        }

        var appointment = await _context.Appointments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .FirstOrDefaultAsync(x =>
                x.Id == appointmentId &&
                x.HospitalId == HospitalId);

        if (appointment == null)
        {
            return NotFound(new { message = "Appointment not found." });
        }

        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")) &&
            appointment.BranchId != BranchId!.Value)
        {
            return Forbid();
        }

        var prescription = await _context.Prescriptions
            .AsNoTracking()
            .Include(x => x.Medicines)
            .Where(x =>
                x.AppointmentId == appointmentId &&
                x.HospitalId == HospitalId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (prescription == null)
        {
            return NotFound(new
            {
                message = "No prescription was found for this appointment."
            });
        }

        return Ok(new
        {
            prescriptionId = prescription.Id,
            appointmentId = prescription.AppointmentId,
            patientId = appointment.PatientId,
            patientName = appointment.Patient?.Name,
            doctorId = appointment.DoctorId,
            doctorName = appointment.Doctor?.Name,
            branchId = appointment.BranchId,
            diagnosis = prescription.Diagnosis,
            instructions = prescription.Instructions,
            followUpDate = prescription.FollowUpDate,
            status = prescription.Status,
            medicines = prescription.Medicines.Select(m => new
            {
                id = m.Id,
                prescriptionItemId = m.Id,
                medicineId = m.MedicineId,
                prescribedQuantity = m.PrescribedQuantity,
                medicineName = m.MedicineName,
                dosage = m.Dosage,
                frequency = m.Frequency,
                duration = m.Duration,
                notes = m.Notes
            })
        });
    }

    // =====================================================
    // GET ACTUALLY DISPENSED MEDICINES FOR PHARMACY BILLING
    // GET /api/Billing/appointments/{appointmentId}/dispensed-medicines
    // This is read-only: quantities and prices come from saved dispense items.
    // =====================================================

    [HttpGet("appointments/{appointmentId:int}/dispensed-medicines")]
    public async Task<IActionResult> GetDispensedMedicines(int appointmentId)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        var appointment = await _context.Appointments
            .AsNoTracking()
            .Where(x => x.Id == appointmentId && x.HospitalId == HospitalId)
            .Select(x => new { x.Id, x.BranchId, x.PatientId })
            .FirstOrDefaultAsync();

        if (appointment == null)
            return NotFound(new { message = "Appointment not found." });

        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")) &&
            appointment.BranchId != BranchId)
            return Forbid();

        var lines = await (
            from prescription in _context.Prescriptions.AsNoTracking()
            join dispense in _context.PharmacyDispenses.AsNoTracking()
                on prescription.Id equals dispense.PrescriptionId
            join item in _context.PharmacyDispenseItems.AsNoTracking()
                on dispense.Id equals item.PharmacyDispenseId
            join prescribed in _context.PrescriptionItems.AsNoTracking()
                on item.PrescriptionItemId equals prescribed.Id
            where prescription.AppointmentId == appointmentId
                && prescription.HospitalId == HospitalId
                && dispense.HospitalId == HospitalId
                && dispense.BranchId == appointment.BranchId
                && prescription.Appointment.BranchId == appointment.BranchId
                && prescribed.PrescriptionId == prescription.Id
            orderby dispense.DispensedAt, item.Id
            select new
            {
                dispenseId = dispense.Id,
                dispenseItemId = item.Id,
                prescriptionId = prescription.Id,
                prescriptionItemId = prescribed.Id,
                medicineName = prescribed.MedicineName,
                prescribedQuantity = prescribed.PrescribedQuantity,
                stockBatchId = item.StockBatchId,
                quantity = item.Quantity,
                unitPrice = item.UnitPrice,
                lineTotal = item.Quantity * item.UnitPrice
            }
        ).ToListAsync();

        if (lines.Count == 0)
            return NotFound(new { message = "No dispensed medicines found for this appointment." });

        return Ok(new
        {
            appointmentId,
            patientId = appointment.PatientId,
            branchId = appointment.BranchId,
            quantitySource = "Saved pharmacy dispensing records",
            totalQuantity = lines.Sum(x => x.quantity),
            medicineCharge = lines.Sum(x => x.lineTotal),
            quantitySummary = lines.GroupBy(x => new { x.prescriptionItemId, x.medicineName, x.prescribedQuantity })
                .Select(g => new
                {
                    g.Key.prescriptionItemId,
                    g.Key.medicineName,
                    g.Key.prescribedQuantity,
                    dispensedQuantity = g.Sum(x => x.quantity),
                    extraQuantity = Math.Max(0, g.Sum(x => x.quantity) - g.Key.prescribedQuantity),
                    additionalQuantity = Math.Max(0, g.Sum(x => x.quantity) - g.Key.prescribedQuantity),
                    remainingUndispensedQuantity = Math.Max(0, g.Key.prescribedQuantity - g.Sum(x => x.quantity)),
                    lineTotal = g.Sum(x => x.lineTotal)
                }).ToList(),
            medicines = lines
        });
    }

    // =====================================================
    // GET BILL BY ID
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBillById(int id)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        var query = _context.Billings
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Appointment)
            .Where(x => x.Id == id && x.HospitalId == HospitalId);

        if (User.IsInRole("Receptionist") || User.IsInRole("Pharmacist"))
        {
            query = query.Where(x => x.BranchId == BranchId!.Value);
        }

        var bill = await query
            .Select(x => new
            {
                x.Id,
                InvoiceNumber = x.InvoiceNumber ?? ("BILL-" + x.Id),
                x.AppointmentId,
                x.PatientId,
                PatientName = x.Patient.Name,
                PatientCode = x.Patient.PatientCode,
                PatientPhone = x.Patient.Phone,
                PatientAge = x.Patient.Age,
                PatientGender = x.Patient.Gender,
                x.DoctorId,
                DoctorName = x.Doctor.Name,
                DoctorDepartment = x.Doctor.Specialization,
                AppointmentTokenNumber = x.Appointment != null ? x.Appointment.TokenNumber : null,
                AppointmentDate = x.Appointment != null ? (DateTime?)x.Appointment.Date : null,
                AppointmentStartTime = x.Appointment != null ? (TimeSpan?)x.Appointment.StartTime : null,
                VisitType = x.Appointment != null ? x.Appointment.BookingType : null,
                AppointmentPaymentStatus = x.Appointment != null ? x.Appointment.PaymentStatus : null,
                AppointmentPaymentMode = x.Appointment != null ? x.Appointment.PaymentMode : null,
                BranchName = x.Branch != null ? x.Branch.Name : null,
                x.BranchId,
                x.BillingType,
                x.ConsultationCharge,
                x.LabCharge,
                x.MedicineCharge,
                x.SubTotal,
                discountPercentage = x.Discount,
                discountAmount = x.DiscountAmount,
                taxableAmount = x.TaxableAmount,
                x.GstPercentage,
                x.GstAmount,
                x.TaxType,
                x.CgstPercentage,
                x.CgstAmount,
                x.SgstPercentage,
                x.SgstAmount,
                x.IgstPercentage,
                x.IgstAmount,
                x.TotalAmount,
                PaidAmount = _context.BillingPaymentTransactions
                    .Where(t => t.BillingId == x.Id && t.HospitalId == HospitalId && t.Status == "Paid")
                    .Sum(t => (decimal?)t.Amount) ?? 0m,
                RefundedAmount = _context.HospitalPaymentRefunds
                    .Where(r => r.BillingId == x.Id && r.HospitalId == HospitalId && r.Status == "processed")
                    .Sum(r => (decimal?)r.Amount) ?? 0m,
                OutstandingAmount = x.TotalAmount
                    - (_context.BillingPaymentTransactions
                        .Where(t => t.BillingId == x.Id && t.HospitalId == HospitalId && t.Status == "Paid")
                        .Sum(t => (decimal?)t.Amount) ?? 0m)
                    + (_context.HospitalPaymentRefunds
                        .Where(r => r.BillingId == x.Id && r.HospitalId == HospitalId && r.Status == "processed")
                        .Sum(r => (decimal?)r.Amount) ?? 0m),
                x.PaymentMode,
                x.Status,
                x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (bill == null)
        {
            return NotFound(new { message = "Bill not found." });
        }
        var pharmacyOnlyError = RestrictPharmacistToPharmacy(bill.BillingType);
        if (pharmacyOnlyError != null) return pharmacyOnlyError;

        // Resolve invoice branding from this bill's hospital only.
        // Settings override hospital defaults when a non-empty value is saved.
        var hospital = await _context.Hospitals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == HospitalId);
        var invoiceSetting = await _context.InvoiceSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.HospitalId == HospitalId);

        static string? Prefer(string? selected, string? fallback) =>
            !string.IsNullOrWhiteSpace(selected) ? selected : fallback;

        var clinic = new
        {
            name = Prefer(invoiceSetting?.ClinicName, hospital?.Name),
            address = Prefer(invoiceSetting?.ClinicAddress, hospital?.Address),
            phone = Prefer(invoiceSetting?.ClinicPhone, hospital?.Phone),
            email = Prefer(invoiceSetting?.ClinicEmail, hospital?.Email),
            gstin = Prefer(invoiceSetting?.GstNumber, hospital?.Gstin),
            registrationNumber = Prefer(invoiceSetting?.RegistrationNumber, hospital?.RegistrationNumber),
            logoUrl = Prefer(invoiceSetting?.LogoUrl, hospital?.LogoUrl),
            headerTitle = Prefer(invoiceSetting?.HeaderTitle, Prefer(invoiceSetting?.ClinicName, hospital?.Name)),
            headerSubtitle = invoiceSetting?.HeaderSubtitle,
            footerNote = invoiceSetting?.FooterNote,
            invoiceTemplate = invoiceSetting?.InvoiceTemplate ?? "Professional",
            accentColor = invoiceSetting?.AccentColor ?? "#0f9d9d",
            city = hospital?.City,
            state = hospital?.State,
            district = hospital?.District,
            country = hospital?.Country,
            postalCode = hospital?.PostalCode
        };

        // For pharmacy invoices, return the actual dispensed lines rather than
        // only the aggregate MedicineCharge. Restrict to this bill's appointment,
        // hospital and branch so another branch's medicines cannot be exposed.
        if (string.Equals(bill.BillingType, "Pharmacy", StringComparison.OrdinalIgnoreCase))
        {
            var medicines = await (
                from prescription in _context.Prescriptions.AsNoTracking()
                join dispense in _context.PharmacyDispenses.AsNoTracking()
                    on prescription.Id equals dispense.PrescriptionId
                join dispenseItem in _context.PharmacyDispenseItems.AsNoTracking()
                    on dispense.Id equals dispenseItem.PharmacyDispenseId
                join prescriptionItem in _context.PrescriptionItems.AsNoTracking()
                    on dispenseItem.PrescriptionItemId equals prescriptionItem.Id
                where prescription.AppointmentId == bill.AppointmentId
                    && prescription.HospitalId == HospitalId
                    && dispense.HospitalId == HospitalId
                    && dispense.BranchId == bill.BranchId
                    && prescription.Appointment.BranchId == bill.BranchId
                    && prescriptionItem.PrescriptionId == prescription.Id
                orderby dispense.DispensedAt, dispenseItem.Id
                select new
                {
                    medicineName = prescriptionItem.MedicineName,
                    prescriptionItemId = prescriptionItem.Id,
                    prescribedQuantity = prescriptionItem.PrescribedQuantity,
                    quantity = dispenseItem.Quantity,
                    unitPrice = dispenseItem.UnitPrice,
                    totalAmount = dispenseItem.Quantity * dispenseItem.UnitPrice
                }
            ).ToListAsync();

            var otcMedicines = await GetOtcLinesAsync(
                bill.AppointmentId, HospitalId, bill.BranchId!.Value, includeCommitted: false);
            var linkedIds = await _context.PharmacyOtcSales.AsNoTracking()
                .Where(x => x.BillingId == bill.Id && x.HospitalId == HospitalId &&
                    x.BranchId == bill.BranchId && x.Status == "Billed")
                .Select(x => x.Id).ToListAsync();
            otcMedicines = otcMedicines.Where(x => linkedIds.Contains(x.OtcSaleId)).ToList();
            var otcCharge = otcMedicines.Sum(x => x.LineTotal);

            return Ok(new
            {
                bill.Id,
                bill.InvoiceNumber,
                bill.AppointmentId,
                bill.PatientId,
                bill.PatientName,
                bill.PatientCode,
                bill.PatientPhone,
                bill.PatientAge,
                bill.PatientGender,
                bill.DoctorId,
                bill.DoctorName,
                bill.DoctorDepartment,
                bill.AppointmentTokenNumber,
                bill.AppointmentDate,
                bill.AppointmentStartTime,
                bill.VisitType,
                bill.AppointmentPaymentStatus,
                bill.AppointmentPaymentMode,
                bill.BranchName,
                bill.BranchId,
                bill.BillingType,
                bill.ConsultationCharge,
                bill.LabCharge,
                bill.MedicineCharge,
                bill.SubTotal,
                bill.discountPercentage,
                bill.discountAmount,
                bill.taxableAmount,
                bill.GstPercentage,
                bill.GstAmount,
                bill.TaxType,
                bill.CgstPercentage,
                bill.CgstAmount,
                bill.SgstPercentage,
                bill.SgstAmount,
                bill.IgstPercentage,
                bill.IgstAmount,
                bill.TotalAmount,
                bill.PaymentMode,
                bill.Status,
                bill.CreatedAt,
                clinic,
                quantitySource = "Saved prescription dispensing and billed OTC sales (live view)",
                quantitiesConfirmed = medicines.Sum(m => m.totalAmount) + otcCharge == bill.MedicineCharge,
                totalDispensedQuantity = medicines.Sum(m => m.quantity),
                dispensingQuantitySummary = medicines.GroupBy(m => new { m.prescriptionItemId, m.medicineName, m.prescribedQuantity })
                    .Select(g => new
                    {
                        g.Key.prescriptionItemId,
                        g.Key.medicineName,
                        g.Key.prescribedQuantity,
                        dispensedQuantity = g.Sum(m => m.quantity),
                        extraQuantity = Math.Max(0, g.Sum(m => m.quantity) - g.Key.prescribedQuantity),
                        additionalQuantity = Math.Max(0, g.Sum(m => m.quantity) - g.Key.prescribedQuantity),
                        remainingUndispensedQuantity = Math.Max(0, g.Key.prescribedQuantity - g.Sum(m => m.quantity))
                    }).ToList(),
                totalOtcQuantity = otcMedicines.Sum(m => m.Quantity),
                otcMedicineCharge = otcCharge,
                otcMedicines,
                medicines
            });
        }

        if (string.Equals(bill.BillingType, "Lab", StringComparison.OrdinalIgnoreCase))
        {
            var tests = await GetLabBillingLinesAsync(bill.AppointmentId, HospitalId);
            return Ok(new
            {
                bill.Id,
                bill.InvoiceNumber,
                bill.AppointmentId,
                bill.PatientId,
                bill.PatientName,
                bill.PatientCode,
                bill.PatientPhone,
                bill.PatientAge,
                bill.PatientGender,
                bill.DoctorId,
                bill.DoctorName,
                bill.DoctorDepartment,
                bill.AppointmentTokenNumber,
                bill.AppointmentDate,
                bill.AppointmentStartTime,
                bill.VisitType,
                bill.AppointmentPaymentStatus,
                bill.AppointmentPaymentMode,
                bill.BranchName,
                bill.BranchId,
                bill.BillingType,
                bill.ConsultationCharge,
                bill.LabCharge,
                bill.MedicineCharge,
                bill.SubTotal,
                bill.discountPercentage,
                bill.discountAmount,
                bill.taxableAmount,
                bill.GstPercentage,
                bill.GstAmount,
                bill.TaxType,
                bill.CgstPercentage,
                bill.CgstAmount,
                bill.SgstPercentage,
                bill.SgstAmount,
                bill.IgstPercentage,
                bill.IgstAmount,
                bill.TotalAmount,
                bill.PaymentMode,
                bill.Status,
                bill.CreatedAt,
                clinic,
                labTests = tests.Select(x => new
                {
                    labOrderId = x.LabOrderId,
                    testCode = x.TestCode,
                    testName = x.TestName,
                    quantity = x.Quantity,
                    unitPrice = x.UnitPrice,
                    lineTotal = x.LineTotal,
                    status = x.Status
                }).ToList(),
                labTotal = tests.Sum(x => x.LineTotal),
                labChargeMatchesCurrentOrders = tests.Sum(x => x.LineTotal) == bill.LabCharge
            });
        }

        // OP invoices retain the existing flat response fields and add clinic branding.
        return Ok(new
        {
            bill.Id,
            bill.InvoiceNumber,
            bill.AppointmentId,
            bill.PatientId,
            bill.PatientName,
            bill.PatientCode,
            bill.PatientPhone,
            bill.PatientAge,
            bill.PatientGender,
            bill.DoctorId,
            bill.DoctorName,
            bill.DoctorDepartment,
            bill.AppointmentTokenNumber,
            bill.AppointmentDate,
            bill.AppointmentStartTime,
            bill.VisitType,
            bill.AppointmentPaymentStatus,
            bill.AppointmentPaymentMode,
            bill.BranchName,
            bill.BranchId,
            bill.BillingType,
            bill.ConsultationCharge,
            bill.LabCharge,
            bill.MedicineCharge,
            bill.SubTotal,
            bill.discountPercentage,
            bill.discountAmount,
            bill.taxableAmount,
            bill.GstPercentage,
            bill.GstAmount,
            bill.TaxType,
            bill.CgstPercentage,
            bill.CgstAmount,
            bill.SgstPercentage,
            bill.SgstAmount,
            bill.IgstPercentage,
            bill.IgstAmount,
            bill.TotalAmount,
            bill.PaymentMode,
            bill.Status,
            bill.CreatedAt,
            clinic
        });

    }

    // =====================================================
    // GET BILLS
    // Supports OP, Lab and Pharmacy filters
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetBills(
        [FromQuery] string? billingType = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null)
    {
        var scopeError = ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        if (User.IsInRole("Pharmacist") && string.IsNullOrWhiteSpace(billingType))
            billingType = "Pharmacy";
        var pharmacyOnlyError = RestrictPharmacistToPharmacy(billingType);
        if (pharmacyOnlyError != null) return pharmacyOnlyError;

        var normalizedType =
            NormalizeBillingType(billingType);

        if (!string.IsNullOrWhiteSpace(billingType) &&
            normalizedType == null)
        {
            return BadRequest(new
            {
                message =
                    "BillingType must be OP, Lab, or Pharmacy."
            });
        }

        var query = _context.Billings
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Appointment)
            .Where(x =>
                x.HospitalId == HospitalId);

        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")))
        {
            query = query.Where(x =>
                x.BranchId == BranchId!.Value);
        }

        if (normalizedType != null)
        {
            query = query.Where(x =>
                x.BillingType == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus =
                status.Trim().ToLower();

            query = query.Where(x =>
                x.Status.ToLower() ==
                normalizedStatus);
        }

        if (date.HasValue)
        {
            var selectedDate =
                date.Value.Date;

            query = query.Where(x =>
                x.CreatedAt.Date ==
                selectedDate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                x.Patient.Name.Contains(term) ||
                x.Patient.PatientCode.Contains(term) ||
                x.Patient.Phone.Contains(term) ||
                x.Doctor.Name.Contains(term));
        }

        var bills = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                billId = x.Id,
                billNumber = x.InvoiceNumber ?? ("BILL-" + x.Id),
                invoiceNumber = x.InvoiceNumber ?? ("BILL-" + x.Id),

                billingType = x.BillingType,

                appointmentId = x.AppointmentId,
                appointmentNumber =
                    x.Appointment != null
                        ? x.Appointment.TokenNumber
                        : null,

                patientId = x.PatientId,
                patientName = x.Patient.Name,
                patientCode = x.Patient.PatientCode,
                patientPhone = x.Patient.Phone,

                doctorId = x.DoctorId,
                doctorName = x.Doctor.Name,

                hospitalId = x.HospitalId,
                branchId = x.BranchId,

                consultationCharge =
                    x.ConsultationCharge,

                labCharge =
                    x.LabCharge,

                medicineCharge =
                    x.MedicineCharge,

                subTotal =
                    x.SubTotal,

                discountPercentage =
                    x.Discount,

                discountAmount = x.DiscountAmount,

                taxableAmount = x.TaxableAmount,

                gstPercentage =
                    x.GstPercentage,

                gstAmount = x.GstAmount,
                taxType = x.TaxType,
                cgstPercentage = x.CgstPercentage,
                cgstAmount = x.CgstAmount,
                sgstPercentage = x.SgstPercentage,
                sgstAmount = x.SgstAmount,
                igstPercentage = x.IgstPercentage,
                igstAmount = x.IgstAmount,

                totalAmount =
                    x.TotalAmount,

                paymentMode =
                    x.PaymentMode,

                status =
                    x.Status,

                createdAt =
                    x.CreatedAt
            })
            .ToListAsync();

        return Ok(bills);
    }

    // =====================================================
    // GET OP BILLS
    // GET /api/Billing/op
    // =====================================================

    [HttpGet("op")]
    public async Task<IActionResult> GetOpBills(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null)
    {
        if (User.IsInRole("Pharmacist")) return StatusCode(403, new { message = "Pharmacists may access Pharmacy billing only." });
        return await GetBillsByType(
            "OP",
            status,
            date,
            search);
    }

    // =====================================================
    // GET LAB BILLS
    // GET /api/Billing/lab
    // =====================================================

    [HttpGet("lab")]
    public async Task<IActionResult> GetLabBills(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null)
    {
        if (User.IsInRole("Pharmacist")) return StatusCode(403, new { message = "Pharmacists may access Pharmacy billing only." });
        return await GetBillsByType(
            "Lab",
            status,
            date,
            search);
    }

    // =====================================================
    // GET PHARMACY BILLS
    // GET /api/Billing/pharmacy
    // =====================================================

    [HttpGet("pharmacy")]
    public async Task<IActionResult> GetPharmacyBills(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null)
    {
        return await GetBillsByType(
            "Pharmacy",
            status,
            date,
            search);
    }

    // =====================================================
    // SHARED GET-BY-TYPE HELPER
    // Not an API endpoint
    // =====================================================

    private async Task<IActionResult> GetBillsByType(
        string billingType,
        string? status = null,
        DateTime? date = null,
        string? search = null)
    {
        var scopeError = ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var pharmacyOnlyError = RestrictPharmacistToPharmacy(billingType);
        if (pharmacyOnlyError != null) return pharmacyOnlyError;

        var normalizedType =
            NormalizeBillingType(billingType);

        if (normalizedType == null)
        {
            return BadRequest(new
            {
                message =
                    "BillingType must be OP, Lab, or Pharmacy."
            });
        }

        var query = _context.Billings
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Appointment)
            .Where(x =>
                x.HospitalId == HospitalId &&
                x.BillingType == normalizedType);

        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")))
        {
            query = query.Where(x =>
                x.BranchId == BranchId!.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus =
                status.Trim().ToLower();

            query = query.Where(x =>
                x.Status.ToLower() ==
                normalizedStatus);
        }

        if (date.HasValue)
        {
            var selectedDate =
                date.Value.Date;

            query = query.Where(x =>
                x.CreatedAt.Date ==
                selectedDate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                x.Patient.Name.Contains(term) ||
                x.Patient.PatientCode.Contains(term) ||
                x.Patient.Phone.Contains(term) ||
                x.Doctor.Name.Contains(term));
        }

        var bills = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                billId = x.Id,
                billNumber = x.InvoiceNumber ?? ("BILL-" + x.Id),
                invoiceNumber = x.InvoiceNumber ?? ("BILL-" + x.Id),

                billingType = x.BillingType,

                appointmentId = x.AppointmentId,
                appointmentNumber =
                    x.Appointment != null
                        ? x.Appointment.TokenNumber
                        : null,

                patientId = x.PatientId,
                patientName = x.Patient.Name,
                patientCode = x.Patient.PatientCode,
                patientPhone = x.Patient.Phone,

                doctorId = x.DoctorId,
                doctorName = x.Doctor.Name,

                hospitalId = x.HospitalId,
                branchId = x.BranchId,

                consultationCharge =
                    x.ConsultationCharge,

                labCharge =
                    x.LabCharge,

                medicineCharge =
                    x.MedicineCharge,

                subTotal =
                    x.SubTotal,

                discountPercentage =
                    x.Discount,

                discountAmount = x.DiscountAmount,

                taxableAmount = x.TaxableAmount,

                gstPercentage =
                    x.GstPercentage,

                gstAmount = x.GstAmount,
                taxType = x.TaxType,
                cgstPercentage = x.CgstPercentage,
                cgstAmount = x.CgstAmount,
                sgstPercentage = x.SgstPercentage,
                sgstAmount = x.SgstAmount,
                igstPercentage = x.IgstPercentage,
                igstAmount = x.IgstAmount,

                totalAmount =
                    x.TotalAmount,

                paymentMode =
                    x.PaymentMode,

                status =
                    x.Status,

                createdAt =
                    x.CreatedAt
            })
            .ToListAsync();

        return Ok(bills);
    }

    // =====================================================
    // CREATE OP BILL
    // POST /api/Billing/op
    // =====================================================

    [HttpPost("op")]
    public async Task<IActionResult> CreateOpBill(
        [FromBody] CreateBillingDto dto, [FromQuery] string? taxType = null)
    {
        if (User.IsInRole("Pharmacist")) return StatusCode(403, new { message = "Pharmacists may access Pharmacy billing only." });
        dto.BillingType = "OP";

        // OP billing should never receive Lab/Pharmacy amounts.
        dto.LabCharge = 0;
        dto.MedicineCharge = 0;

        return await CreateBill(dto, taxType);
    }

    // =====================================================
    // CREATE LAB BILL
    // POST /api/Billing/lab
    // =====================================================

    [HttpPost("lab")]
    public async Task<IActionResult> CreateLabBill(
        [FromBody] CreateBillingDto dto, [FromQuery] string? taxType = null)
    {
        if (User.IsInRole("Pharmacist")) return StatusCode(403, new { message = "Pharmacists may access Pharmacy billing only." });
        dto.BillingType = "Lab";

        // Lab amount is always calculated by the backend
        // from the prescribed LabOrders for this appointment.
        dto.ConsultationCharge = 0;
        dto.LabCharge = 0;
        dto.MedicineCharge = 0;

        return await CreateBill(dto, taxType);
    }

    // =====================================================
    // CREATE PHARMACY BILL
    // POST /api/Billing/pharmacy
    // =====================================================

    [HttpPost("pharmacy")]
    public async Task<IActionResult> CreatePharmacyBill(
        [FromBody] CreateBillingDto dto, [FromQuery] string? taxType = null)
    {
        dto.BillingType = "Pharmacy";

        // Pharmacy billing should never receive OP/Lab amounts.
        dto.ConsultationCharge = 0;
        dto.LabCharge = 0;

        return await CreateBill(dto, taxType);
    }

    // =====================================================
    // CREATE BILL (GENERIC / BACKWARD COMPATIBILITY)
    // POST /api/Billing
    // =====================================================

    [HttpPost]
    public async Task<IActionResult> CreateBill(
        [FromBody] CreateBillingDto dto, [FromQuery] string? taxType = null)
    {
        var scopeError = ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        // Tax settings are resolved from server configuration below.
        // Legacy taxType query parameter is ignored on creation.

        var billingType =
            NormalizeBillingType(dto.BillingType);

        var pharmacyOnlyError = RestrictPharmacistToPharmacy(billingType);
        if (pharmacyOnlyError != null) return pharmacyOnlyError;

        if (billingType == null)
        {
            return BadRequest(new
            {
                message =
                    "BillingType must be OP, Lab, or Pharmacy."
            });
        }

        if (dto.AppointmentId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Valid AppointmentId is required."
            });
        }

        var appointment =
            await _context.Appointments
                .Include(x => x.Doctor)
                .Include(x => x.Patient)
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.AppointmentId &&
                    x.HospitalId == HospitalId);

        if (appointment == null)
        {
            return NotFound(new
            {
                message =
                    "Appointment not found."
            });
        }

        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")) &&
            appointment.BranchId != BranchId!.Value)
        {
            return Forbid();
        }

        // Keep one bill of each type per appointment.
        // OP is normally created automatically when consultation payment succeeds.
        // Lab and Pharmacy are created after the doctor orders tests/medicines.
        var existingBill = await _context.Billings
            .AsNoTracking()
            .AnyAsync(x =>
                x.AppointmentId == appointment.Id &&
                x.HospitalId == HospitalId &&
                x.BillingType == billingType);

        if (existingBill)
        {
            var existingInvoice = await _context.Billings.AsNoTracking()
                .Where(x => x.AppointmentId == appointment.Id &&
                    x.HospitalId == HospitalId && x.BillingType == billingType)
                .Select(x => new { x.Id, x.InvoiceNumber, x.Status, x.TotalAmount, x.BranchId })
                .FirstAsync();
            return Conflict(new
            {
                message = $"A {billingType} bill already exists for this appointment. Do not create another bill; retrieve the existing invoice and collect payment if appropriate.",
                billId = existingInvoice.Id,
                invoiceNumber = existingInvoice.InvoiceNumber ?? ("BILL-" + existingInvoice.Id),
                appointmentId = appointment.Id,
                branchId = existingInvoice.BranchId,
                status = existingInvoice.Status,
                totalAmount = existingInvoice.TotalAmount,
                existingBill = true
            });
        }

        decimal consultationCharge = 0;
        decimal labCharge = 0;
        decimal medicineCharge = 0;
        List<PharmacyBillingLine> pharmacyLines = new();
        List<OtcBillingLine> otcLines = new();

        switch (billingType)
        {
            case "OP":
                consultationCharge =
                    dto.ConsultationCharge > 0
                        ? dto.ConsultationCharge
                        : appointment.Doctor.Fees;
                break;

            case "Lab":
                // Do not accept the Lab amount from the frontend.
                // Calculate it from the tests prescribed for this appointment.
                labCharge = await _context.LabOrders
                    .AsNoTracking()
                    .Where(x =>
                        x.AppointmentId == appointment.Id &&
                        x.HospitalId == HospitalId &&
                        x.Status != "Cancelled")
                    .SumAsync(x => (decimal?)x.Price) ?? 0m;

                if (labCharge <= 0)
                {
                    return BadRequest(new
                    {
                        message = "No prescribed Lab tests with a valid price were found for this appointment."
                    });
                }

                break;

            case "Pharmacy":
                // BranchId is nullable on Appointment; validate before using it.
                if (!appointment.BranchId.HasValue)
                {
                    return BadRequest(new
                    {
                        message = "Appointment is not assigned to a valid branch."
                    });
                }

                pharmacyLines = await GetPharmacyBillingLinesAsync(
                    appointment.Id, HospitalId, appointment.BranchId.Value);
                otcLines = (await GetOtcLinesAsync(
                    appointment.Id, HospitalId, appointment.BranchId.Value))
                    .Where(x => x.OtcSaleId > 0).ToList();
                var committedSaleIds = await _context.PharmacyOtcSales.AsNoTracking()
                    .Where(x => x.AppointmentId == appointment.Id &&
                        x.HospitalId == HospitalId && x.BranchId == appointment.BranchId &&
                        x.Status == "Committed" && x.BillingId == null)
                    .Select(x => x.Id).ToListAsync();
                otcLines = otcLines.Where(x => committedSaleIds.Contains(x.OtcSaleId)).ToList();

                var quantityLimitError = ValidatePrescriptionDispensingLimits(pharmacyLines);
                if (quantityLimitError != null)
                    return BadRequest(quantityLimitError);

                // A previously billed OTC sale must never be charged in a new invoice.
                if (otcLines.Count == 0 && pharmacyLines.Count == 0)
                    return BadRequest(new { message = "No saved prescription dispensing or committed OTC medicines found. Confirm dispensing or commit the OTC sale first." });

                if (otcLines.Any(x => x.Quantity <= 0 || x.UnitPrice < 0))
                    return BadRequest(new { message = "An OTC sale has invalid quantities or prices." });

                // Each saved dispense batch allocation is billed once. ExtraQuantity
                // is reporting metadata only; never add it to the charged quantity.
                // Stock is already deducted by the dispensing/OTC confirmation APIs.
                medicineCharge = pharmacyLines.Sum(x => x.LineTotal)
                    + otcLines.Sum(x => x.LineTotal);
                if (medicineCharge <= 0)
                    return BadRequest(new { message = "The medicine charge must be positive." });
                break;
        }

        var subTotal =
            consultationCharge +
            labCharge +
            medicineCharge;

        if (dto.Discount < 0 ||
            dto.Discount > 100)
        {
            return BadRequest(new
            {
                message = "Discount percentage must be between 0 and 100."
            });
        }

        // Discount is stored as percentage.
        // Example: Discount = 10 means 10%.
        var discountPercentage = dto.Discount;

        // Never accept Paid from a bill-creation request.
        // Only PATCH /api/Billing/{billId}/payment can record payment.
        var status = "Pending";

        // Backend-owned tax settings; supplied dto.GstPercentage and query taxType
        // are not authoritative for new invoices.
        var configuredRate = GetConfiguredGstRate(billingType);
        var configuredTaxType = GetConfiguredTaxType(billingType, configuredRate);

        var bill = new Billing
        {
            AppointmentId =
                appointment.Id,

            PatientId =
                appointment.PatientId,

            DoctorId =
                appointment.DoctorId,

            HospitalId =
                HospitalId,

            BranchId =
                appointment.BranchId,

            BillingType =
                billingType,

            ConsultationCharge =
                consultationCharge,

            LabCharge =
                labCharge,

            MedicineCharge =
                medicineCharge,

            SubTotal =
                subTotal,

            Discount =
                discountPercentage,

            GstPercentage =
                configuredRate,

            GstAmount = 0m,

            TotalAmount = 0m,

            PaymentMode =
                string.IsNullOrWhiteSpace(
                    dto.PaymentMode)
                    ? "Cash"
                    : dto.PaymentMode.Trim(),

            Status =
                status,

            CreatedAt =
                DateTime.UtcNow
        };

        ApplyTaxBreakdown(bill, configuredTaxType);

        // Save the invoice and attach OTC sales atomically, preventing partial billing.
        await using (var billingTx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable))
        {
            if (billingType == "Pharmacy")
            {
                // Quantities may have been adjusted after the initial read.
                // Refuse to generate an invoice using stale quantities or prices.
                var currentDispensingLines = await GetPharmacyBillingLinesAsync(
                    appointment.Id, HospitalId, appointment.BranchId!.Value);
                var currentQuantityLimitError = ValidatePrescriptionDispensingLimits(currentDispensingLines);
                if (currentQuantityLimitError != null)
                    return BadRequest(currentQuantityLimitError);

                // Compare saved dispensing lines as a multiset, not by a partial sort.
                // Multiple dispense records can share the same prescription item and
                // stock batch. Sorting only on those two fields leaves tied rows in
                // unspecified order and can incorrectly produce a 409 even when the
                // quantities and prices are identical.
                var originalLines = pharmacyLines
                    .GroupBy(x => new { x.PrescriptionItemId, x.StockBatchId, x.Quantity, x.UnitPrice })
                    .Select(g => new
                    {
                        g.Key.PrescriptionItemId,
                        g.Key.StockBatchId,
                        g.Key.Quantity,
                        g.Key.UnitPrice,
                        Count = g.Count()
                    })
                    .OrderBy(x => x.PrescriptionItemId)
                    .ThenBy(x => x.StockBatchId)
                    .ThenBy(x => x.Quantity)
                    .ThenBy(x => x.UnitPrice)
                    .ToList();

                var latestLines = currentDispensingLines
                    .GroupBy(x => new { x.PrescriptionItemId, x.StockBatchId, x.Quantity, x.UnitPrice })
                    .Select(g => new
                    {
                        g.Key.PrescriptionItemId,
                        g.Key.StockBatchId,
                        g.Key.Quantity,
                        g.Key.UnitPrice,
                        Count = g.Count()
                    })
                    .OrderBy(x => x.PrescriptionItemId)
                    .ThenBy(x => x.StockBatchId)
                    .ThenBy(x => x.Quantity)
                    .ThenBy(x => x.UnitPrice)
                    .ToList();

                if (currentDispensingLines.Sum(x => x.LineTotal) != pharmacyLines.Sum(x => x.LineTotal) ||
                    !originalLines.SequenceEqual(latestLines))
                {
                    _logger.LogWarning(
                        "Pharmacy billing data changed for AppointmentId {AppointmentId}, HospitalId {HospitalId}, BranchId {BranchId}. Original lines {OriginalCount}, latest lines {LatestCount}.",
                        appointment.Id, HospitalId, appointment.BranchId,
                        pharmacyLines.Count, currentDispensingLines.Count);
                    return Conflict(new
                    {
                        message = "Saved dispensing records changed during billing. Refresh the dispensed medicines and retry.",
                        appointmentId = appointment.Id,
                        originalLineCount = pharmacyLines.Count,
                        latestLineCount = currentDispensingLines.Count
                    });
                }

                var duplicate = await _context.Billings.AnyAsync(x =>
                    x.AppointmentId == appointment.Id && x.HospitalId == HospitalId &&
                    x.BillingType == "Pharmacy");
                if (duplicate)
                    return Conflict(new { message = "A pharmacy bill was created concurrently for this appointment. Refresh the invoice list." });

                var otcSaleIds = otcLines.Select(x => x.OtcSaleId).Distinct().ToList();
                var sales = await _context.PharmacyOtcSales.Where(x =>
                    otcSaleIds.Contains(x.Id) && x.HospitalId == HospitalId &&
                    x.BranchId == appointment.BranchId && x.AppointmentId == appointment.Id &&
                    x.Status == "Committed" && x.BillingId == null).ToListAsync();
                if (sales.Count != otcSaleIds.Count)
                    return Conflict(new { message = "One or more OTC sales are no longer available for billing. Refresh and retry." });

                _context.Billings.Add(bill);
                await _context.SaveChangesAsync();
                bill.InvoiceNumber = $"BILL-{bill.Id}";
                foreach (var sale in sales)
                {
                    sale.BillingId = bill.Id;
                    sale.Status = "Billed";
                }
            }
            else
            {
                _context.Billings.Add(bill);
                await _context.SaveChangesAsync();
                bill.InvoiceNumber = $"BILL-{bill.Id}";
            }
            await _context.SaveChangesAsync();
            await billingTx.CommitAsync();
        }

        // Create a patient-specific notification for every generated bill.
        try
        {
            var notification = new Notification
            {
                PatientId = bill.PatientId,
                Title = $"{bill.BillingType} Bill Generated",
                Message = $"{bill.BillingType} bill {bill.InvoiceNumber ?? $"BILL-{bill.Id}"} has been generated. Total amount: ₹{bill.TotalAmount:F2}.",
                IsRead = false,
                IsSent = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bill {BillId} saved but patient notification failed.", bill.Id);
            foreach (var entry in _context.ChangeTracker.Entries<Notification>()
                .Where(x => x.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
        }

        // Keep the existing patient-portal notification above. In addition,
        // notify active Admins assigned to this pharmacy/hospital.
        await NotifyPharmacyAdminAboutBillAsync(bill);

        return Ok(new
        {
            message =
                $"{billingType} bill created successfully.",

            billId =
                bill.Id,

            invoiceNumber = bill.InvoiceNumber,

            billingType =
                bill.BillingType,

            appointmentId =
                bill.AppointmentId,

            patientId =
                bill.PatientId,

            branchId =
                bill.BranchId,

            consultationCharge =
                bill.ConsultationCharge,

            labCharge =
                bill.LabCharge,

            medicineCharge =
                bill.MedicineCharge,

            labTests = bill.BillingType == "Lab"
                ? (await GetLabBillingLinesAsync(bill.AppointmentId, HospitalId))
                    .Select(x => new
                    {
                        labOrderId = x.LabOrderId,
                        testCode = x.TestCode,
                        testName = x.TestName,
                        quantity = x.Quantity,
                        unitPrice = x.UnitPrice,
                        lineTotal = x.LineTotal,
                        status = x.Status
                    }).ToArray()
                : null,

            medicineChargeSource = bill.BillingType == "Pharmacy"
                ? "Saved prescription dispensing and committed OTC sales"
                : null,
            quantitiesConfirmed = bill.BillingType == "Pharmacy" ? true : (bool?)null,
            quantitySource = bill.BillingType == "Pharmacy" ? "Saved prescription dispensing and committed OTC sales" : null,
            totalDispensedQuantity = bill.BillingType == "Pharmacy"
                ? pharmacyLines.Sum(x => x.Quantity) : (int?)null,
            dispensingQuantitySummary = bill.BillingType == "Pharmacy"
                ? GetDispensingQuantitySummary(pharmacyLines) : null,
            medicines = bill.BillingType == "Pharmacy"
                ? pharmacyLines.Select(x => new
                {
                    x.PrescriptionId,
                    x.PrescriptionItemId,
                    x.MedicineName,
                    x.PrescribedQuantity,
                    x.StockBatchId,
                    x.Quantity,
                    x.UnitPrice,
                    lineTotal = x.LineTotal
                }).ToArray()
                : null,

            otcMedicineCharge = bill.BillingType == "Pharmacy"
                ? otcLines.Sum(x => x.LineTotal) : (decimal?)null,
            totalOtcQuantity = bill.BillingType == "Pharmacy"
                ? otcLines.Sum(x => x.Quantity) : (int?)null,
            otcMedicines = bill.BillingType == "Pharmacy" ? otcLines : null,

            subTotal =
                bill.SubTotal,

            discountPercentage =
                bill.Discount,

            discountAmount = bill.DiscountAmount,
            taxableAmount = bill.TaxableAmount,
            gstPercentage = bill.GstPercentage,
            gstAmount = bill.GstAmount,
            taxType = bill.TaxType,
            cgstPercentage = bill.CgstPercentage,
            cgstAmount = bill.CgstAmount,
            sgstPercentage = bill.SgstPercentage,
            sgstAmount = bill.SgstAmount,
            igstPercentage = bill.IgstPercentage,
            igstAmount = bill.IgstAmount,
            totalAmount = bill.TotalAmount,

            paymentMode =
                bill.PaymentMode,

            status =
                bill.Status
        });
    }

    // =====================================================
    // UPDATE PAYMENT STATUS
    // =====================================================

    [HttpPatch("{billId:int}/payment")]
    public async Task<IActionResult> UpdatePayment(int billId, [FromBody] PatientPortalPaymentDto dto)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;
        if (dto == null) return BadRequest(new { message = "Payment details are required." });

        var method = dto.PaymentMode?.Trim();
        var allowed = new[] { "Cash", "UPI", "Card" };
        var normalizedMethod = allowed.FirstOrDefault(x =>
            x.Equals(method, StringComparison.OrdinalIgnoreCase));
        if (normalizedMethod == null)
            return BadRequest(new { message = "PaymentMode must be Cash, UPI or Card." });

        // Cash requires an explicit payment confirmation, not a reference number.
        // For electronic payments, keep the gateway/bank reference for reconciliation.
        if (normalizedMethod != "Cash" && string.IsNullOrWhiteSpace(dto.ReferenceNumber))
            return BadRequest(new { message = "Transaction reference is required for UPI or Card payments." });

        // Serialize concurrent payment requests to prevent duplicate ledger entries.
        await using var tx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var bill = await _context.Billings
            .Include(x => x.Appointment)
            .FirstOrDefaultAsync(x => x.Id == billId && x.HospitalId == HospitalId);
        if (bill == null) return NotFound(new { message = "Bill not found." });
        var pharmacyOnlyError = RestrictPharmacistToPharmacy(bill.BillingType);
        if (pharmacyOnlyError != null) return pharmacyOnlyError;
        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")) && bill.BranchId != BranchId)
            return Forbid();
        if (bill.TotalAmount <= 0)
            return BadRequest(new { message = "Bill total must be greater than zero." });

        var alreadyCollected = await _context.BillingPaymentTransactions
            .Where(x => x.BillingId == bill.Id && x.HospitalId == HospitalId && x.Status == "Paid")
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var refunded = await _context.HospitalPaymentRefunds
            .Where(x => x.BillingId == bill.Id && x.HospitalId == HospitalId && x.Status == "processed")
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var outstanding = Math.Max(0m, bill.TotalAmount - alreadyCollected + refunded);
        if (outstanding <= 0m)
            return Conflict(new { message = "This bill is already fully paid.", outstandingAmount = 0m });
        var amountToCollect = dto.Amount ?? outstanding;
        if (amountToCollect <= 0m || amountToCollect > outstanding || decimal.Round(amountToCollect, 2) != amountToCollect)
            return BadRequest(new { message = "Payment amount must be positive and cannot exceed the outstanding balance.", outstandingAmount = outstanding });

        var now = DateTime.UtcNow;
        var payment = new BillingPaymentTransaction
        {
            BillingId = bill.Id,
            HospitalId = bill.HospitalId,
            BranchId = bill.BranchId,
            Amount = amountToCollect,
            PaymentMethod = normalizedMethod,
            ReferenceNumber = dto.ReferenceNumber?.Trim(),
            Status = "Paid",
            PaidAt = now,
            CreatedAt = now
        };
        _context.BillingPaymentTransactions.Add(payment);
        var newCollected = alreadyCollected + amountToCollect - refunded;
        bill.Status = newCollected >= bill.TotalAmount ? "Paid" : "PartiallyPaid";
        bill.PaymentMode = normalizedMethod;

        if (bill.Status == "Paid" && bill.BillingType == "OP" && bill.Appointment != null)
        {
            bill.Appointment.PaymentStatus = "Paid";
            if (bill.Appointment.Status == "Pending")
                bill.Appointment.Status = "Confirmed";
        }

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new
        {
            message = "Payment recorded successfully.",
            billId = bill.Id,
            paymentId = payment.Id,
            billingType = bill.BillingType,
            amount = payment.Amount,
            outstandingAmount = Math.Max(0m, bill.TotalAmount - newCollected),
            status = bill.Status,
            paymentMode = bill.PaymentMode,
            referenceNumber = payment.ReferenceNumber
        });
    }

    [HttpGet("{billId:int}/payments")]
    public async Task<IActionResult> GetPaymentHistory(int billId)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;
        var bill = await _context.Billings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == billId && x.HospitalId == HospitalId);
        if (bill == null) return NotFound(new { message = "Bill not found." });
        var pharmacyOnlyError = RestrictPharmacistToPharmacy(bill.BillingType);
        if (pharmacyOnlyError != null) return pharmacyOnlyError;
        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")) && bill.BranchId != BranchId)
            return Forbid();
        var payments = await _context.BillingPaymentTransactions.AsNoTracking()
            .Where(x => x.BillingId == billId && x.HospitalId == HospitalId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new {
                x.Id,
                x.BillingId,
                x.Amount,
                x.PaymentMethod,
                x.ReferenceNumber,
                x.Status,
                x.CreatedAt,
                x.PaidAt
            })
            .ToListAsync();
        return Ok(new { billId, payments });
    }


    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateBill(int id, [FromBody] CreateBillingDto dto, [FromQuery] string? taxType = null)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;
        if (dto == null) return BadRequest(new { message = "Billing details are required." });

        var normalizedTaxType = NormalizeTaxType(taxType);
        if (normalizedTaxType == null)
            return BadRequest(new { message = "TaxType must be CGST_SGST, IGST, None or Unspecified." });
        if (normalizedTaxType == "None" && dto.GstPercentage > 0m)
            return BadRequest(new { message = "TaxType None requires GST percentage zero." });

        var bill = await _context.Billings
            .Include(x => x.Appointment)
            .Include(x => x.Doctor)
            .FirstOrDefaultAsync(x => x.Id == id && x.HospitalId == HospitalId);

        if (bill == null)
            return NotFound(new { message = "Bill not found." });

        var pharmacyOnlyError = RestrictPharmacistToPharmacy(bill.BillingType);
        if (pharmacyOnlyError != null) return pharmacyOnlyError;

        var hasCollectedPayment = await _context.BillingPaymentTransactions
            .AnyAsync(x => x.BillingId == bill.Id && x.HospitalId == HospitalId && x.Status == "Paid");
        if (hasCollectedPayment)
            return Conflict(new { message = "Invoices with payment allocations cannot be edited. Record an adjustment or refund instead." });

        if (bill.BillingType == "Pharmacy" &&
            !string.Equals(dto.BillingType, "Pharmacy", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "A pharmacy bill cannot be converted to another type." });

        if (bill.BillingType != "Pharmacy" &&
            string.Equals(dto.BillingType, "Pharmacy", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Create pharmacy bills using POST /api/Billing/pharmacy." });

        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")) &&
            bill.BranchId != BranchId!.Value)
        {
            return Forbid();
        }

        var allowed = new[] { "OP", "Lab", "Pharmacy" };

        var type = allowed.FirstOrDefault(x =>
            x.Equals(dto.BillingType, StringComparison.OrdinalIgnoreCase));

        if (type == null)
            return BadRequest(new
            {
                message = "BillingType must be OP, Lab or Pharmacy."
            });

        bill.BillingType = type;

        bill.ConsultationCharge = type == "OP"
            ? (dto.ConsultationCharge > 0
                ? dto.ConsultationCharge
                : bill.Doctor?.Fees ?? 0)
            : 0;

        if (type == "Lab")
        {
            var calculatedLabCharge = await _context.LabOrders
                .AsNoTracking()
                .Where(x =>
                    x.AppointmentId == bill.AppointmentId &&
                    x.HospitalId == HospitalId &&
                    x.Status != "Cancelled")
                .SumAsync(x => (decimal?)x.Price) ?? 0m;

            if (calculatedLabCharge <= 0)
            {
                return BadRequest(new
                {
                    message = "No prescribed Lab tests with a valid price were found for this appointment."
                });
            }

            bill.LabCharge = calculatedLabCharge;
        }
        else
        {
            bill.LabCharge = 0;
        }

        if (type == "Pharmacy")
        {
            if (!bill.BranchId.HasValue)
                return BadRequest(new { message = "Pharmacy bill has no branch." });
            var savedLines = await GetPharmacyBillingLinesAsync(
                bill.AppointmentId, HospitalId, bill.BranchId.Value);
            var updateQuantityLimitError = ValidatePrescriptionDispensingLimits(savedLines);
            if (updateQuantityLimitError != null)
                return BadRequest(updateQuantityLimitError);
            var dispensed = savedLines.Count > 0
                ? savedLines.Sum(x => x.LineTotal)
                : (decimal?)null;
            var linkedOtc = await _context.PharmacyOtcSales.AsNoTracking()
                .Where(x => x.BillingId == bill.Id && x.HospitalId == HospitalId &&
                    x.BranchId == bill.BranchId && x.Status == "Billed")
                .SelectMany(x => x.Items)
                .SumAsync(x => (decimal?)(x.Quantity * x.UnitPrice)) ?? 0m;
            var calculatedTotal = (dispensed ?? 0m) + linkedOtc;
            if (calculatedTotal <= 0)
                return BadRequest(new { message = "No dispensed or billed OTC medicines were found for this appointment." });
            bill.MedicineCharge = calculatedTotal;
        }
        else
        {
            bill.MedicineCharge = 0;
        }

        bill.SubTotal =
            bill.ConsultationCharge +
            bill.LabCharge +
            bill.MedicineCharge;

        if (dto.Discount < 0 ||
            dto.Discount > 100)
        {
            return BadRequest(new
            {
                message = "Discount percentage must be between 0 and 100."
            });
        }

        // Discount is stored as percentage.
        if (dto.GstPercentage < 0 || dto.GstPercentage > 100)
            return BadRequest(new { message = "GST percentage must be between 0 and 100." });

        bill.Discount = dto.Discount;
        bill.GstPercentage = dto.GstPercentage;

        ApplyTaxBreakdown(bill, normalizedTaxType);

        bill.PaymentMode =
            dto.PaymentMode ?? bill.PaymentMode;

        // Payment status is controlled only by PATCH /api/Billing/{billId}/payment.
        if (string.Equals(dto.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Use the payment endpoint to mark a bill as paid." });

        bill.Status = "Pending";

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Bill updated successfully.",
            bill.Id,
            bill.BillingType,
            bill.SubTotal,
            discountPercentage = bill.Discount,
            discountAmount = bill.DiscountAmount,
            taxableAmount = bill.TaxableAmount,
            bill.GstPercentage,
            bill.GstAmount,
            bill.TaxType,
            bill.CgstPercentage,
            bill.CgstAmount,
            bill.SgstPercentage,
            bill.SgstAmount,
            bill.IgstPercentage,
            bill.IgstAmount,
            bill.TotalAmount,
            bill.Status
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBill(int id)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        var bill = await _context.Billings
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.HospitalId == HospitalId);

        if (bill == null)
            return NotFound(new
            {
                message = "Bill not found."
            });

        if ((User.IsInRole("Receptionist") || User.IsInRole("Pharmacist")) &&
            bill.BranchId != BranchId!.Value)
        {
            return Forbid();
        }

        var pharmacyOnlyError = RestrictPharmacistToPharmacy(bill.BillingType);
        if (pharmacyOnlyError != null) return pharmacyOnlyError;

        var hasCollectedPayment = await _context.BillingPaymentTransactions
            .AnyAsync(x => x.BillingId == bill.Id && x.HospitalId == HospitalId && x.Status == "Paid");
        if (hasCollectedPayment)
            return Conflict(new { message = "Invoices with payment allocations cannot be deleted. Record an adjustment or refund instead." });

        if (bill.BillingType == "Pharmacy")
            return Conflict(new { message = "Pharmacy bills linked to dispensing cannot be deleted. Use a controlled cancellation/refund workflow." });

        _context.Billings.Remove(bill);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Bill deleted successfully."
        });
    }
}
