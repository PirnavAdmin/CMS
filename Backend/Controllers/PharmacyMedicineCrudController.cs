
using System;
using System.Linq;
using System.Text.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy/medicines")]
[Authorize(Roles = "Admin,Pharmacist")]
public class PharmacyMedicineCrudController : ControllerBase
{
    // Matches the existing CMS permission module.
    private const string PermissionModule = "Medicines";

    private readonly AppDbContext _db;

    public PharmacyMedicineCrudController(AppDbContext db)
    {
        _db = db;
    }

    private int? ClaimInt(string type)
    {
        return int.TryParse(
            User.FindFirstValue(type),
            out var value
        ) ? value : null;
    }

    // =====================================================
    // AUTHORIZATION
    // =====================================================

    private async Task<bool> HasPermissionAsync(string action)
    {
        var userId = ClaimInt(ClaimTypes.NameIdentifier);
        var hospitalId = ClaimInt("HospitalId");

        var role = User.FindFirstValue(ClaimTypes.Role)
                   ?? User.FindFirstValue("role");

        if (userId == null || hospitalId == null)
            return false;

        var account = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.Id == userId.Value &&
                u.HospitalId == hospitalId.Value &&
                u.IsActive &&
                u.Role == role);

        if (account == null)
            return false;

        // Pharmacist permissions.
        if (role == "Pharmacist")
        {
            if (account.BranchId == null)
                return false;

            var branchExists = await _db.Branches
                .AsNoTracking()
                .AnyAsync(b =>
                    b.Id == account.BranchId.Value &&
                    b.HospitalId == hospitalId.Value);

            if (!branchExists)
                return false;

            var permission = await _db.UserPermissions
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.UserId == userId.Value &&
                    p.HospitalId == hospitalId.Value &&
                    p.Module == PermissionModule);

            if (permission == null)
                return false;

            return action switch
            {
                "view" => permission.CanView,
                "create" => permission.CanCreate,
                "edit" => permission.CanEdit,
                "delete" => permission.CanDelete,
                _ => false
            };
        }

        // Admin permissions.
        if (role == "Admin")
        {
            var permission = await _db.RolePermissions
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.AdminUserId == userId.Value &&
                    p.HospitalId == hospitalId.Value &&
                    p.RoleName == "Admin" &&
                    p.Module == PermissionModule);

            if (permission == null)
                return false;

            return action switch
            {
                "view" => permission.CanView,
                "create" => permission.CanCreate,
                "edit" => permission.CanEdit,
                "delete" => permission.CanDelete,
                _ => false
            };
        }

        return false;
    }

    private async Task<IActionResult?> CheckAsync(string action)
    {
        if (ClaimInt("HospitalId") == null ||
            ClaimInt(ClaimTypes.NameIdentifier) == null)
        {
            return Unauthorized(new
            {
                message = "Missing user or hospital identity in token"
            });
        }

        if (!await HasPermissionAsync(action))
        {
            return StatusCode(403, new
            {
                message = $"Permission denied: {PermissionModule}.{action}"
            });
        }

        return null;
    }

    // =====================================================
    // RESPONSE MAPPING
    // =====================================================

    private static MedicineCrudResponseDto Map(Medicine m)
    {
        return new MedicineCrudResponseDto
        {
            Id = m.Id,
            MedicineCode = m.MedicineCode,
            Name = m.Name,
            GenericName = m.GenericName,
            BrandName = m.BrandName,
            Category = m.Category,
            DosageForm = m.DosageForm,
            Strength = m.Strength,
            Manufacturer = m.Manufacturer,
            PackSize = m.PackSize,
            RequiresPrescription = m.RequiresPrescription,
            IsActive = m.IsActive,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt,
            DeactivatedAt = m.DeactivatedAt,
            ReactivatedAt = m.ReactivatedAt
        };
    }

    // =====================================================
    // GET MEDICINE BY ID
    // GET /api/pharmacy/medicines/{id}
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var denied = await CheckAsync("view");
        if (denied != null)
            return denied;

        var hospitalId = ClaimInt("HospitalId")!.Value;

        var medicine = await _db.Medicines
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.Id == id &&
                m.HospitalId == hospitalId);

        if (medicine == null)
        {
            return NotFound(new
            {
                message = "Medicine not found"
            });
        }

        return Ok(Map(medicine));
    }

    // =====================================================
    // CREATE MEDICINE
    // POST /api/pharmacy/medicines
    // =====================================================

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMedicineCrudDto dto)
    {
        var denied = await CheckAsync("create");
        if (denied != null) return denied;

        var hospitalId = ClaimInt("HospitalId")!.Value;
        var userId = ClaimInt(ClaimTypes.NameIdentifier)!.Value;
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var code = dto.MedicineCode?.Trim() ?? "";
        var name = dto.Name?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Medicine code and name are required." });

        if (await _db.Medicines.AnyAsync(m => m.HospitalId == hospitalId && m.MedicineCode == code))
            return Conflict(new { message = "Medicine code already exists in this hospital." });

        // The batch is optional for legacy master-only medicine creation.
        // A batch is required when any batch/stock/supplier/pricing field is supplied.
        var hasBatchData = dto.BranchId.HasValue || dto.BatchNumber != null ||
            dto.ManufactureDate.HasValue || dto.ExpiryDate.HasValue ||
            dto.StockQuantity.HasValue || dto.SupplierCode != null ||
            dto.SupplierName != null || dto.PurchasePrice.HasValue ||
            dto.SellingPrice.HasValue || dto.GST.HasValue ||
            dto.MinimumStock.HasValue || dto.MaximumStock.HasValue ||
            dto.ReorderLevel.HasValue || dto.StorageLocation != null;

        int? branchId = null;
        if (hasBatchData)
        {
            if (string.IsNullOrWhiteSpace(dto.BatchNumber) || !dto.ExpiryDate.HasValue ||
                !dto.SellingPrice.HasValue || !dto.StockQuantity.HasValue)
                return BadRequest(new { message = "BatchNumber, ExpiryDate, SellingPrice and StockQuantity are required when creating stock." });

            if (dto.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
                return BadRequest(new { message = "ExpiryDate cannot be in the past." });

            if (dto.ManufactureDate.HasValue && dto.ManufactureDate.Value.Date > dto.ExpiryDate.Value.Date)
                return BadRequest(new { message = "ManufactureDate must not be after ExpiryDate." });

            if (dto.MinimumStock.HasValue && dto.MaximumStock.HasValue &&
                dto.MinimumStock.Value > dto.MaximumStock.Value)
                return BadRequest(new { message = "MinimumStock cannot exceed MaximumStock." });

            if (dto.StockQuantity < 0 || dto.SellingPrice < 0 || dto.PurchasePrice < 0 ||
                dto.GST < 0 || dto.GST > 100 || dto.MinimumStock < 0 ||
                dto.MaximumStock < 0 || dto.ReorderLevel < 0)
                return BadRequest(new { message = "Quantity, prices and stock limits must be nonnegative; GST must be 0–100." });

            if (role == "Pharmacist")
            {
                var account = await _db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId && u.HospitalId == hospitalId);
                if (account?.BranchId == null)
                    return StatusCode(403, new { message = "Pharmacist has no assigned branch." });
                branchId = account.BranchId.Value;
                if (dto.BranchId.HasValue && dto.BranchId.Value != branchId.Value)
                    return StatusCode(403, new { message = "Cannot create stock in another branch." });
            }
            else if (role == "Admin")
            {
                if (!dto.BranchId.HasValue)
                    return BadRequest(new { message = "BranchId is required for Admin stock creation." });
                branchId = dto.BranchId.Value;
            }
            else
                return Forbid();

            var validBranch = await _db.Branches.AsNoTracking()
                .AnyAsync(b => b.Id == branchId.Value && b.HospitalId == hospitalId);
            if (!validBranch)
                return BadRequest(new { message = "Branch does not belong to this hospital." });
        }

        var medicine = new Medicine
        {
            HospitalId = hospitalId,
            MedicineCode = code,
            Name = name,
            GenericName = dto.GenericName,
            BrandName = dto.BrandName,
            Category = dto.Category,
            DosageForm = dto.DosageForm,
            Strength = dto.Strength,
            Manufacturer = dto.Manufacturer,
            PackSize = dto.PackSize,
            RequiresPrescription = dto.RequiresPrescription,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Both inserts are committed together, or neither is committed.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Medicines.Add(medicine);
            await _db.SaveChangesAsync();

            PharmacyStockBatch? batch = null;
            if (hasBatchData)
            {
                batch = new PharmacyStockBatch
                {
                    HospitalId = hospitalId,
                    BranchId = branchId!.Value,
                    MedicineId = medicine.Id,
                    BatchNumber = dto.BatchNumber!.Trim(),
                    Quantity = dto.StockQuantity!.Value,
                    SellingPrice = dto.SellingPrice!.Value,
                    ExpiryDate = dto.ExpiryDate!.Value,
                    ManufactureDate = dto.ManufactureDate,
                    SupplierCode = dto.SupplierCode?.Trim(),
                    SupplierName = dto.SupplierName?.Trim(),
                    PurchasePrice = dto.PurchasePrice,
                    GST = dto.GST,
                    MinimumStock = dto.MinimumStock,
                    MaximumStock = dto.MaximumStock,
                    ReorderLevel = dto.ReorderLevel,
                    StorageLocation = dto.StorageLocation?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                _db.PharmacyStockBatches.Add(batch);
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            // Preserve the original POST response contract for existing frontend clients.
            return CreatedAtAction(nameof(GetById), new { id = medicine.Id }, Map(medicine));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Conflict(new { message = "Unable to save medicine and stock batch. Check database constraints." });
        }
    }

    // =====================================================
    // PUT - UPDATE MEDICINE AND SELECTED STOCK BATCH
    // =====================================================
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMedicineCrudDto dto)
    {
        var denied = await CheckAsync("edit");
        if (denied != null) return denied;

        var hospitalId = ClaimInt("HospitalId")!.Value;
        var userId = ClaimInt(ClaimTypes.NameIdentifier)!.Value;
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");

        var medicine = await _db.Medicines.FirstOrDefaultAsync(m =>
            m.Id == id && m.HospitalId == hospitalId);
        if (medicine == null) return NotFound(new { message = "Medicine not found." });
        if (!medicine.IsActive)
            return Conflict(new { message = "Reactivate medicine before editing." });

        var code = dto.MedicineCode?.Trim() ?? "";
        var name = dto.Name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Medicine code and name are required." });

        if (await _db.Medicines.AnyAsync(m =>
            m.HospitalId == hospitalId && m.Id != id && m.MedicineCode == code))
            return Conflict(new { message = "Medicine code already exists in this hospital." });

        // A legacy medicine-master-only PUT remains supported.
        var hasBatchData = dto.BatchId.HasValue || dto.BranchId.HasValue ||
            dto.BatchNumber != null || dto.ManufactureDate.HasValue ||
            dto.ExpiryDate.HasValue || dto.StockQuantity.HasValue ||
            dto.SupplierCode != null || dto.SupplierName != null ||
            dto.PurchasePrice.HasValue || dto.SellingPrice.HasValue ||
            dto.GST.HasValue || dto.MinimumStock.HasValue ||
            dto.MaximumStock.HasValue || dto.ReorderLevel.HasValue ||
            dto.StorageLocation != null;

        PharmacyStockBatch? batch = null;
        if (hasBatchData)
        {
            int? branchId = dto.BranchId;
            if (role == "Pharmacist")
            {
                var account = await _db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId && u.HospitalId == hospitalId);
                if (account?.BranchId == null)
                    return StatusCode(403, new { message = "Pharmacist has no assigned branch." });
                if (branchId.HasValue && branchId.Value != account.BranchId.Value)
                    return StatusCode(403, new { message = "Cannot edit another branch's stock." });
                branchId = account.BranchId.Value;
            }
            else if (role != "Admin") return Forbid();

            if (branchId.HasValue && !await _db.Branches.AsNoTracking()
                .AnyAsync(b => b.Id == branchId.Value && b.HospitalId == hospitalId))
                return BadRequest(new { message = "Invalid branch for this hospital." });

            // Never guess when several batches exist. BatchId is preferred;
            // otherwise match the existing batch number within the branch.
            if (dto.BatchId.HasValue)
            {
                batch = await _db.PharmacyStockBatches.FirstOrDefaultAsync(b =>
                    b.Id == dto.BatchId.Value && b.MedicineId == id &&
                    b.HospitalId == hospitalId &&
                    (!branchId.HasValue || b.BranchId == branchId.Value));
            }
            else if (!string.IsNullOrWhiteSpace(dto.BatchNumber))
            {
                var batchNumber = dto.BatchNumber.Trim();
                var matches = await _db.PharmacyStockBatches.Where(b =>
                    b.MedicineId == id && b.HospitalId == hospitalId &&
                    b.BatchNumber == batchNumber &&
                    (!branchId.HasValue || b.BranchId == branchId.Value))
                    .Take(2).ToListAsync();
                if (matches.Count > 1)
                    return Conflict(new { message = "Multiple batches match. Send batchId and branchId." });
                batch = matches.SingleOrDefault();
            }
            else
            {
                return BadRequest(new { message = "Send batchId (preferred) or existing batchNumber to edit batch fields." });
            }

            if (batch == null)
                return NotFound(new { message = "Matching stock batch not found. Check batchId, branchId and medicineId." });

            // Stock changes need the stock-adjustment/dispensing workflow.
            if (dto.StockQuantity.HasValue && dto.StockQuantity.Value != batch.Quantity)
                return BadRequest(new
                {
                    message = "StockQuantity cannot be changed through medicine PUT. Use an audited stock adjustment API."
                });

            if (dto.ExpiryDate.HasValue && dto.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
                return BadRequest(new { message = "ExpiryDate cannot be in the past." });

            var effectiveExpiry = dto.ExpiryDate ?? batch.ExpiryDate;
            var effectiveMfg = dto.ManufactureDate ?? batch.ManufactureDate;
            if (effectiveMfg.HasValue && effectiveMfg.Value.Date > effectiveExpiry.Date)
                return BadRequest(new { message = "ManufactureDate cannot be after ExpiryDate." });

            var min = dto.MinimumStock ?? batch.MinimumStock;
            var max = dto.MaximumStock ?? batch.MaximumStock;
            if (min.HasValue && max.HasValue && min.Value > max.Value)
                return BadRequest(new { message = "MinimumStock cannot exceed MaximumStock." });

            if (dto.BatchNumber != null && string.IsNullOrWhiteSpace(dto.BatchNumber))
                return BadRequest(new { message = "BatchNumber cannot be empty." });

            if (dto.BatchNumber != null && dto.BatchNumber.Trim() != batch.BatchNumber &&
                await _db.PharmacyStockBatches.AnyAsync(b =>
                    b.Id != batch.Id && b.HospitalId == hospitalId &&
                    b.BranchId == batch.BranchId && b.MedicineId == id &&
                    b.BatchNumber == dto.BatchNumber.Trim()))
                return Conflict(new { message = "Batch number already exists for this medicine and branch." });
        }

        // Update master and batch in the same SaveChanges operation.
        medicine.MedicineCode = code;
        medicine.Name = name;
        medicine.GenericName = dto.GenericName;
        medicine.BrandName = dto.BrandName;
        medicine.Category = dto.Category;
        medicine.DosageForm = dto.DosageForm;
        medicine.Strength = dto.Strength;
        medicine.Manufacturer = dto.Manufacturer;
        medicine.PackSize = dto.PackSize;
        medicine.RequiresPrescription = dto.RequiresPrescription;
        medicine.UpdatedAt = DateTime.UtcNow;

        if (batch != null)
        {
            if (dto.BatchNumber != null) batch.BatchNumber = dto.BatchNumber.Trim();
            if (dto.ManufactureDate.HasValue) batch.ManufactureDate = dto.ManufactureDate;
            if (dto.ExpiryDate.HasValue) batch.ExpiryDate = dto.ExpiryDate.Value;
            if (dto.SupplierCode != null) batch.SupplierCode = dto.SupplierCode.Trim();
            if (dto.SupplierName != null) batch.SupplierName = dto.SupplierName.Trim();
            if (dto.PurchasePrice.HasValue) batch.PurchasePrice = dto.PurchasePrice.Value;
            if (dto.SellingPrice.HasValue) batch.SellingPrice = dto.SellingPrice.Value;
            if (dto.GST.HasValue) batch.GST = dto.GST.Value;
            if (dto.MinimumStock.HasValue) batch.MinimumStock = dto.MinimumStock.Value;
            if (dto.MaximumStock.HasValue) batch.MaximumStock = dto.MaximumStock.Value;
            if (dto.ReorderLevel.HasValue) batch.ReorderLevel = dto.ReorderLevel.Value;
            if (dto.StorageLocation != null) batch.StorageLocation = dto.StorageLocation.Trim();
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Unable to update medicine and batch. Check database constraints." });
        }

        // Preserve the original response shape for existing frontend clients.
        return Ok(Map(medicine));
    }

    // =====================================================
    // PATCH - UPDATE SELECTED MEDICINE MASTER FIELDS
    // PATCH /api/pharmacy/medicines/{id}
    // =====================================================
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> PatchMedicine(int id, [FromBody] JsonElement changes)
    {
        var denied = await CheckAsync("edit");
        if (denied != null) return denied;
        if (changes.ValueKind != JsonValueKind.Object)
            return BadRequest(new { message = "Expected a JSON object." });

        var allowed = new[] { "medicineCode", "name", "genericName", "brandName",
            "category", "dosageForm", "strength", "manufacturer", "packSize", "requiresPrescription" };
        var fields = changes.EnumerateObject().ToList();
        if (fields.Count == 0)
            return BadRequest(new { message = "At least one field is required." });
        if (fields.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            return BadRequest(new { message = "Duplicate fields are not allowed." });
        if (fields.Any(x => !allowed.Contains(x.Name, StringComparer.OrdinalIgnoreCase)))
            return BadRequest(new { message = "One or more fields are not editable through this endpoint." });

        var hospitalId = ClaimInt("HospitalId")!.Value;
        var medicine = await _db.Medicines.FirstOrDefaultAsync(m => m.Id == id && m.HospitalId == hospitalId);
        if (medicine == null) return NotFound(new { message = "Medicine not found." });
        if (!medicine.IsActive) return Conflict(new { message = "Reactivate medicine before editing." });

        // Validate every field before applying any changes.
        foreach (var field in fields)
        {
            if (field.Name.Equals("requiresPrescription", StringComparison.OrdinalIgnoreCase))
            {
                if (field.Value.ValueKind != JsonValueKind.True && field.Value.ValueKind != JsonValueKind.False)
                    return BadRequest(new { message = "requiresPrescription must be boolean." });
                continue;
            }
            if (field.Value.ValueKind != JsonValueKind.String && field.Value.ValueKind != JsonValueKind.Null)
                return BadRequest(new { message = $"{field.Name} must be a string or null." });
            var value = field.Value.ValueKind == JsonValueKind.Null ? null : field.Value.GetString()?.Trim();
            if ((field.Name.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                 field.Name.Equals("medicineCode", StringComparison.OrdinalIgnoreCase)) &&
                string.IsNullOrWhiteSpace(value))
                return BadRequest(new { message = $"{field.Name} cannot be empty." });
            var max = field.Name.ToLowerInvariant() switch
            {
                "medicinecode" => 50,
                "name" => 200,
                "genericname" => 200,
                "brandname" => 200,
                "category" => 100,
                "dosageform" => 100,
                "strength" => 100,
                "manufacturer" => 200,
                "packsize" => 100,
                _ => 0
            };
            if (value != null && value.Length > max)
                return BadRequest(new { message = $"{field.Name} exceeds {max} characters." });
        }

        foreach (var field in fields)
        {
            var value = field.Value.ValueKind == JsonValueKind.Null ? null :
                field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString()?.Trim() : null;
            switch (field.Name.ToLowerInvariant())
            {
                case "medicinecode":
                    if (await _db.Medicines.AnyAsync(m => m.HospitalId == hospitalId &&
                        m.Id != id && m.MedicineCode == value))
                        return Conflict(new { message = "Medicine code already exists in this hospital." });
                    medicine.MedicineCode = value!; break;
                case "name": medicine.Name = value!; break;
                case "genericname": medicine.GenericName = value; break;
                case "brandname": medicine.BrandName = value; break;
                case "category": medicine.Category = value; break;
                case "dosageform": medicine.DosageForm = value; break;
                case "strength": medicine.Strength = value; break;
                case "manufacturer": medicine.Manufacturer = value; break;
                case "packsize": medicine.PackSize = value; break;
                case "requiresprescription": medicine.RequiresPrescription = field.Value.GetBoolean(); break;
            }
        }
        medicine.UpdatedAt = DateTime.UtcNow;
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Unable to update medicine. Check database constraints." });
        }
        return Ok(Map(medicine));
    }

    // =====================================================
    // SOFT DELETE MEDICINE
    // DELETE /api/pharmacy/medicines/{id}
    // =====================================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var denied = await CheckAsync("delete");
        if (denied != null)
            return denied;

        var hospitalId = ClaimInt("HospitalId")!.Value;

        var medicine = await _db.Medicines
            .FirstOrDefaultAsync(m =>
                m.Id == id &&
                m.HospitalId == hospitalId);

        if (medicine == null)
        {
            return NotFound(new
            {
                message = "Medicine not found"
            });
        }

        if (!medicine.IsActive)
        {
            return Conflict(new
            {
                message = "Medicine is already inactive"
            });
        }

        var now = DateTime.UtcNow;

        medicine.IsActive = false;
        medicine.DeactivatedAt = now;
        medicine.UpdatedAt = now;

        await _db.SaveChangesAsync();

        return Ok(Map(medicine));
    }

    // =====================================================
    // REACTIVATE MEDICINE
    // PATCH /api/pharmacy/medicines/{id}/activate
    //
    // Uses CanEdit permission.
    // =====================================================

    [HttpPatch("{id:int}/activate")]
    public async Task<IActionResult> Reactivate(int id)
    {
        var denied = await CheckAsync("edit");
        if (denied != null)
            return denied;

        var hospitalId = ClaimInt("HospitalId")!.Value;

        var medicine = await _db.Medicines
            .FirstOrDefaultAsync(m =>
                m.Id == id &&
                m.HospitalId == hospitalId);

        if (medicine == null)
        {
            return NotFound(new
            {
                message = "Medicine not found"
            });
        }

        if (medicine.IsActive)
        {
            return Conflict(new
            {
                message = "Medicine is already active"
            });
        }

        var now = DateTime.UtcNow;

        medicine.IsActive = true;
        medicine.ReactivatedAt = now;
        medicine.UpdatedAt = now;

        await _db.SaveChangesAsync();

        return Ok(Map(medicine));
    }
}
