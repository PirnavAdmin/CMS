using System.Data;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

public sealed class PharmacyOtcSalesService
{
    private readonly AppDbContext _db;
    public PharmacyOtcSalesService(AppDbContext db) => _db = db;

    public async Task<PharmacyOtcSaleResponseDto> CreateDraftAsync(
        CreatePharmacyOtcSaleDto request, User pharmacist, CancellationToken ct)
    {
        var (hospitalId, branchId) = RequireScope(pharmacist);
        if (request == null || request.AppointmentId <= 0 || request.Items == null || request.Items.Count == 0)
            throw new ArgumentException("Appointment and at least one medicine are required.");
        if (request.Items.Any(x => x.MedicineId <= 0 || x.Quantity <= 0))
            throw new ArgumentException("Medicine IDs and quantities must be positive.");
        if (request.Items.GroupBy(x => x.MedicineId).Any(g => g.Count() > 1))
            throw new ArgumentException("Combine duplicate medicines into one request item.");

        bool appointmentExists = await _db.Appointments.AsNoTracking().AnyAsync(
            a => a.Id == request.AppointmentId && a.HospitalId == hospitalId && a.BranchId == branchId, ct);
        if (!appointmentExists)
            throw new ArgumentException("Appointment not found in your assigned branch.");

        bool existing = await _db.PharmacyOtcSales.AnyAsync(
            s => s.HospitalId == hospitalId && s.BranchId == branchId &&
                 s.AppointmentId == request.AppointmentId && s.Status == "Draft", ct);
        if (existing)
            throw new InvalidOperationException("An OTC draft already exists for this appointment. Review it before creating another.");

        var sale = new PharmacyOtcSale
        {
            HospitalId = hospitalId,
            BranchId = branchId,
            AppointmentId = request.AppointmentId,
            PharmacistUserId = pharmacist.Id,
            Status = "Draft"
        };

        foreach (var line in request.Items)
        {
            var medicine = await _db.Medicines.AsNoTracking().FirstOrDefaultAsync(
                m => m.Id == line.MedicineId && m.HospitalId == hospitalId && m.IsActive, ct);
            if (medicine == null)
                throw new ArgumentException($"Medicine {line.MedicineId} is not active in this pharmacy.");
            if (medicine.RequiresPrescription)
                throw new InvalidOperationException($"{medicine.Name} requires a prescription; OTC sale is not permitted.");

            var now = DateTime.UtcNow;
            // The current draft schema allocates one batch per item.
            var batch = await _db.PharmacyStockBatches.AsNoTracking()
                .Where(b => b.HospitalId == hospitalId && b.BranchId == branchId &&
                            b.MedicineId == line.MedicineId && b.Quantity >= line.Quantity &&
                            b.ExpiryDate >= now.Date && b.SellingPrice >= 0)
                .OrderBy(b => b.ExpiryDate).ThenBy(b => b.Id)
                .FirstOrDefaultAsync(ct);
            if (batch == null)
                throw new InvalidOperationException($"No single unexpired stock batch has {line.Quantity} units of {medicine.Name}.");

            sale.Items.Add(new PharmacyOtcSaleItem
            {
                MedicineId = medicine.Id,
                StockBatchId = batch.Id,
                Quantity = line.Quantity,
                UnitPrice = batch.SellingPrice,
                GstPercentage = batch.GST ?? 0m
            });
        }

        _db.PharmacyOtcSales.Add(sale);
        await _db.SaveChangesAsync(ct);
        return ToResponse(sale);
    }

    public async Task<PharmacyOtcSaleResponseDto?> GetAsync(int id, User pharmacist, CancellationToken ct)
    {
        if (!pharmacist.HospitalId.HasValue || !pharmacist.BranchId.HasValue) return null;
        var sale = await _db.PharmacyOtcSales.AsNoTracking().Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id && s.HospitalId == pharmacist.HospitalId.Value &&
                s.BranchId == pharmacist.BranchId.Value, ct);
        return sale == null ? null : ToResponse(sale);
    }

    // Confirms an OTC sale and deducts inventory exactly once.
    // This method does NOT generate a bill, take payment, or mark a bill Paid.
    // The controller should call this from POST /api/pharmacy/otc-sales/{id}/confirm.
    public async Task<PharmacyOtcSaleResponseDto> ConfirmSaleAsync(
        int id, User pharmacist, CancellationToken ct)
    {
        var (hospitalId, branchId) = RequireScope(pharmacist);
        if (id <= 0) throw new ArgumentException("A valid OTC sale ID is required.");

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        var sale = await _db.PharmacyOtcSales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id && s.HospitalId == hospitalId &&
                s.BranchId == branchId, ct);

        if (sale == null)
            throw new KeyNotFoundException("OTC sale not found in your assigned branch.");
        if (sale.Status != "Draft")
            throw new InvalidOperationException("Only a Draft OTC sale can be confirmed. Stock was not deducted again.");
        if (sale.Items.Count == 0)
            throw new InvalidOperationException("The OTC draft has no items.");

        bool appointmentExists = await _db.Appointments.AsNoTracking().AnyAsync(
            a => a.Id == sale.AppointmentId && a.HospitalId == hospitalId && a.BranchId == branchId, ct);
        if (!appointmentExists)
            throw new InvalidOperationException("The appointment no longer belongs to this branch.");

        // Do not add an OTC charge to an invoice that was already generated.
        bool existingPharmacyBill = await _db.Billings.AsNoTracking().AnyAsync(
            b => b.AppointmentId == sale.AppointmentId && b.HospitalId == hospitalId &&
                 b.BranchId == branchId && b.BillingType == "Pharmacy", ct);
        if (existingPharmacyBill)
            throw new InvalidOperationException("A pharmacy bill already exists for this appointment. Resolve billing before confirming this OTC draft.");

        var medicineIds = sale.Items.Select(i => i.MedicineId).Distinct().ToList();
        var eligibleIds = await _db.Medicines.AsNoTracking()
            .Where(m => m.HospitalId == hospitalId && m.IsActive &&
                        !m.RequiresPrescription && medicineIds.Contains(m.Id))
            .Select(m => m.Id).ToListAsync(ct);
        if (eligibleIds.Count != medicineIds.Count)
            throw new InvalidOperationException("A medicine is inactive, belongs to another pharmacy, or now requires a prescription.");

        if (sale.Items.Any(i => i.Quantity <= 0 || i.StockBatchId <= 0))
            throw new InvalidOperationException("The draft contains invalid stock allocations.");

        var batchIds = sale.Items.Select(i => i.StockBatchId).Distinct().ToList();
        var batches = await _db.PharmacyStockBatches
            .Where(b => batchIds.Contains(b.Id) && b.HospitalId == hospitalId && b.BranchId == branchId)
            .ToDictionaryAsync(b => b.Id, ct);
        var now = DateTime.UtcNow;

        // Validate all allocations before making any changes.
        foreach (var group in sale.Items.GroupBy(i => i.StockBatchId))
        {
            if (!batches.TryGetValue(group.Key, out var batch))
                throw new InvalidOperationException($"Stock batch {group.Key} is missing from this branch.");
            if (batch.ExpiryDate.Date < now.Date)
                throw new InvalidOperationException($"Stock batch {group.Key} is expired.");
            if (group.Any(i => i.MedicineId != batch.MedicineId))
                throw new InvalidOperationException($"Stock batch {group.Key} does not match the medicine.");
            long requested = group.Sum(i => (long)i.Quantity);
            if (batch.Quantity < requested)
                throw new InvalidOperationException($"Insufficient stock in batch {group.Key}. Requested {requested}, available {batch.Quantity}.");
        }

        foreach (var group in sale.Items.GroupBy(i => i.StockBatchId))
            batches[group.Key].Quantity -= checked((int)group.Sum(i => (long)i.Quantity));

        // Committed means inventory was deducted; it does NOT mean paid or billed.
        sale.Status = "Committed";
        sale.CommittedAt = now;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToResponse(sale);
    }

    private static (int hospitalId, int branchId) RequireScope(User pharmacist)
    {
        if (pharmacist == null || pharmacist.Role != "Pharmacist" || !pharmacist.IsActive ||
            !pharmacist.HospitalId.HasValue || !pharmacist.BranchId.HasValue)
            throw new InvalidOperationException("An active pharmacist assigned to a pharmacy branch is required.");
        return (pharmacist.HospitalId.Value, pharmacist.BranchId.Value);
    }

    private static PharmacyOtcSaleResponseDto ToResponse(PharmacyOtcSale sale) => new()
    {
        Id = sale.Id,
        AppointmentId = sale.AppointmentId,
        BranchId = sale.BranchId,
        Status = sale.Status,
        CreatedAt = sale.CreatedAt,
        Items = sale.Items.Select(i => new PharmacyOtcSaleItemResponseDto
        {
            MedicineId = i.MedicineId,
            StockBatchId = i.StockBatchId,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            GstPercentage = i.GstPercentage
        }).ToList()
    };
}
