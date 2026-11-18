using AuthDemo.Data;
using AuthDemo.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy/medicines")]
[Authorize(Roles = "Pharmacist,Doctor")]
public class PharmacyMedicineImportController : ControllerBase
{
    private readonly AppDbContext _db;

    public PharmacyMedicineImportController(AppDbContext db) => _db = db;

    private static string Normalize(string value) =>
        new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    [HttpPost("import")]
    [Authorize(Roles = "Pharmacist")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Please upload an Excel file.");
        if (file.Length > 10_000_000)
            return BadRequest("Maximum file size is 10 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .xlsx files are supported.");

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            return Unauthorized();

        var pharmacist = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == userId && x.Role == "Pharmacist" && x.IsActive);
        if (pharmacist == null || !pharmacist.HospitalId.HasValue || !pharmacist.BranchId.HasValue)
            return Forbid();

        int hospitalId = pharmacist.HospitalId.Value;
        int branchId = pharmacist.BranchId.Value;
        if (!await _db.Branches.AnyAsync(x => x.Id == branchId && x.HospitalId == hospitalId))
            return BadRequest("Invalid pharmacist branch.");

        var rows = new List<ImportRow>();
        var errors = new List<string>();
        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheet(1);
            var header = sheet.FirstRowUsed();
            if (header == null) return BadRequest("Excel worksheet is empty.");

            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in header.CellsUsed())
            {
                string key = Normalize(cell.GetString());
                if (key.Length == 0) continue;
                if (!columns.TryAdd(key, cell.Address.ColumnNumber))
                    return BadRequest(new { message = $"Duplicate Excel heading: {cell.GetString()}" });
            }

            // Accept both the original six-column template and the 22-column template.
            // "Quantity" and "Stock Quantity" are aliases.
            string[] required = { "medicinecode", "medicinename", "batchnumber", "sellingprice", "expirydate" };
            var missing = required.Where(x => !columns.ContainsKey(x)).ToList();
            if (!columns.ContainsKey("quantity") && !columns.ContainsKey("stockquantity"))
                missing.Add("quantity (or stockquantity)");
            if (missing.Count > 0)
                return BadRequest(new { message = "Missing Excel columns", missing });

            string Cell(IXLRow row, string key)
                => columns.TryGetValue(key, out int index) ? row.Cell(index).GetFormattedString().Trim() : "";

            string CellAny(IXLRow row, params string[] keys)
            {
                foreach (string key in keys)
                    if (columns.ContainsKey(key)) return Cell(row, key);
                return "";
            }

            string? OptionalText(IXLRow row, string key, int max)
            {
                string value = Cell(row, key);
                if (string.IsNullOrWhiteSpace(value)) return null;
                if (value.Length > max) throw new FormatException($"{key} exceeds {max} characters.");
                return value;
            }

            decimal ParseDecimal(string value, string field)
            {
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                    throw new FormatException($"Invalid {field}.");
                return result;
            }

            decimal? OptionalDecimal(IXLRow row, string key, decimal max)
            {
                string value = Cell(row, key);
                if (string.IsNullOrWhiteSpace(value)) return null;
                decimal number = ParseDecimal(value, key);
                if (number < 0 || number > max || decimal.Round(number, 2) != number)
                    throw new FormatException($"{key} must be between 0 and {max}, with up to two decimals.");
                return number;
            }

            int? OptionalInt(IXLRow row, string key)
            {
                string value = Cell(row, key);
                if (string.IsNullOrWhiteSpace(value)) return null;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < 0)
                    throw new FormatException($"{key} must be a non-negative whole number.");
                return number;
            }

            DateTime? OptionalDate(IXLRow row, string key)
            {
                if (!columns.TryGetValue(key, out int index)) return null;
                var cell = row.Cell(index);
                if (cell.IsEmpty()) return null;
                if (cell.TryGetValue<DateTime>(out var date)) return date.Date;
                string value = cell.GetFormattedString().Trim();
                if (string.IsNullOrWhiteSpace(value)) return null;
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    return date.Date;
                if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out date))
                    return date.Date;
                throw new FormatException($"Invalid {key}.");
            }

            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
            {
                if (row.IsEmpty()) continue;
                try
                {
                    string code = Cell(row, "medicinecode");
                    string name = Cell(row, "medicinename");
                    string batch = Cell(row, "batchnumber");
                    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(batch))
                        throw new FormatException("Medicine code, name and batch are required.");
                    if (code.Length > 50 || name.Length > 200 || batch.Length > 100)
                        throw new FormatException("Medicine code, name or batch is too long.");

                    if (!int.TryParse(CellAny(row, "stockquantity", "quantity"), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int quantity) || quantity <= 0)
                        throw new FormatException("Quantity must be a whole number greater than zero.");

                    decimal price = ParseDecimal(Cell(row, "sellingprice"), "sellingprice");
                    if (price < 0 || price > 9999999999999999.99m || decimal.Round(price, 2) != price)
                        throw new FormatException("Invalid sellingprice.");

                    var expiry = OptionalDate(row, "expirydate")
                        ?? throw new FormatException("Expiry date is required.");
                    if (expiry.Date <= DateTime.UtcNow.Date)
                        throw new FormatException("Medicine batch is expired.");
                    var manufacture = OptionalDate(row, "manufacturedate");
                    if (manufacture.HasValue && manufacture.Value.Date > expiry.Date)
                        throw new FormatException("Manufacture date cannot be after expiry date.");

                    var minimum = OptionalInt(row, "minimumstock");
                    var maximum = OptionalInt(row, "maximumstock");
                    var reorder = OptionalInt(row, "reorderlevel");
                    if (minimum.HasValue && maximum.HasValue && minimum > maximum)
                        throw new FormatException("Minimum stock cannot exceed maximum stock.");

                    rows.Add(new ImportRow(
                        code, name, OptionalText(row, "genericname", 200),
                        OptionalText(row, "brandname", 200), OptionalText(row, "category", 100),
                        OptionalText(row, "dosageform", 100), OptionalText(row, "strength", 100),
                        OptionalText(row, "manufacturer", 200), OptionalText(row, "packsize", 100),
                        OptionalText(row, "suppliercode", 50), OptionalText(row, "suppliername", 200),
                        batch, manufacture, expiry.Date, quantity,
                        OptionalDecimal(row, "purchaseprice", 9999999999999999.99m), price,
                        OptionalDecimal(row, "gst", 999.99m), minimum, maximum, reorder,
                        OptionalText(row, "storagelocation", 200)));
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                {
                    errors.Add($"Row {row.RowNumber()}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Unable to read Excel file.", detail = ex.Message });
        }

        if (errors.Count > 0)
            return BadRequest(new { message = "Excel validation failed. No rows imported.", errors });
        if (rows.Count == 0) return BadRequest("No medicine records found.");

        var nameConflicts = rows.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => g.Key).ToArray();
        if (nameConflicts.Length > 0)
            return BadRequest(new { message = "Medicine codes have conflicting names.", medicineCodes = nameConflicts });

        // Same medicine code must not carry conflicting master attributes in one file.
        var masterConflicts = rows.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => string.Join("|", x.GenericName, x.BrandName, x.Category,
                x.DosageForm, x.Strength, x.Manufacturer, x.PackSize)).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => g.Key).ToArray();
        if (masterConflicts.Length > 0)
            return BadRequest(new { message = "Conflicting medicine master details in Excel.", medicineCodes = masterConflicts });

        int medicinesCreated = 0, medicinesUpdated = 0, batchesCreated = 0, batchesUpdated = 0, duplicatesSkipped = 0;
        // Do not apply the same batch twice in one upload.
        var processedBatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            foreach (var row in rows)
            {
                var medicine = await _db.Medicines.FirstOrDefaultAsync(x =>
                    x.HospitalId == hospitalId && x.MedicineCode == row.Code);
                if (medicine == null)
                {
                    medicine = new Medicine
                    {
                        MedicineCode = row.Code,
                        Name = row.Name,
                        HospitalId = hospitalId,
                        IsActive = true,
                        GenericName = row.GenericName,
                        BrandName = row.BrandName,
                        Category = row.Category,
                        DosageForm = row.DosageForm,
                        Strength = row.Strength,
                        Manufacturer = row.Manufacturer,
                        PackSize = row.PackSize
                    };
                    _db.Medicines.Add(medicine);
                    await _db.SaveChangesAsync();
                    medicinesCreated++;
                }
                else
                {
                    // Fill missing master details only; never overwrite existing non-empty values.
                    bool changed = false;
                    if (medicine.GenericName == null && row.GenericName != null) { medicine.GenericName = row.GenericName; changed = true; }
                    if (medicine.BrandName == null && row.BrandName != null) { medicine.BrandName = row.BrandName; changed = true; }
                    if (medicine.Category == null && row.Category != null) { medicine.Category = row.Category; changed = true; }
                    if (medicine.DosageForm == null && row.DosageForm != null) { medicine.DosageForm = row.DosageForm; changed = true; }
                    if (medicine.Strength == null && row.Strength != null) { medicine.Strength = row.Strength; changed = true; }
                    if (medicine.Manufacturer == null && row.Manufacturer != null) { medicine.Manufacturer = row.Manufacturer; changed = true; }
                    if (medicine.PackSize == null && row.PackSize != null) { medicine.PackSize = row.PackSize; changed = true; }
                    if (changed) { await _db.SaveChangesAsync(); medicinesUpdated++; }
                }

                string batchKey = $"{medicine.Id}:{row.Batch}";
                if (!processedBatches.Add(batchKey))
                {
                    duplicatesSkipped++;
                    continue;
                }

                var existingBatch = await _db.PharmacyStockBatches.FirstOrDefaultAsync(x =>
                    x.HospitalId == hospitalId && x.BranchId == branchId &&
                    x.MedicineId == medicine.Id && x.BatchNumber == row.Batch);
                if (existingBatch != null)
                {
                    // Backfill missing metadata only. Never reset current stock, selling price,
                    // or expiry date during a historical re-import.
                    bool changed = false;
                    if (string.IsNullOrWhiteSpace(existingBatch.SupplierCode) && row.SupplierCode != null)
                    { existingBatch.SupplierCode = row.SupplierCode; changed = true; }
                    if (string.IsNullOrWhiteSpace(existingBatch.SupplierName) && row.SupplierName != null)
                    { existingBatch.SupplierName = row.SupplierName; changed = true; }
                    if (!existingBatch.ManufactureDate.HasValue && row.ManufactureDate.HasValue)
                    { existingBatch.ManufactureDate = row.ManufactureDate; changed = true; }
                    if (!existingBatch.PurchasePrice.HasValue && row.PurchasePrice.HasValue)
                    { existingBatch.PurchasePrice = row.PurchasePrice; changed = true; }
                    if (!existingBatch.GST.HasValue && row.GST.HasValue)
                    { existingBatch.GST = row.GST; changed = true; }
                    if (!existingBatch.MinimumStock.HasValue && row.MinimumStock.HasValue)
                    { existingBatch.MinimumStock = row.MinimumStock; changed = true; }
                    if (!existingBatch.MaximumStock.HasValue && row.MaximumStock.HasValue)
                    { existingBatch.MaximumStock = row.MaximumStock; changed = true; }
                    if (!existingBatch.ReorderLevel.HasValue && row.ReorderLevel.HasValue)
                    { existingBatch.ReorderLevel = row.ReorderLevel; changed = true; }
                    if (string.IsNullOrWhiteSpace(existingBatch.StorageLocation) && row.StorageLocation != null)
                    { existingBatch.StorageLocation = row.StorageLocation; changed = true; }

                    if (changed)
                    {
                        await _db.SaveChangesAsync();
                        batchesUpdated++;
                    }
                    else duplicatesSkipped++;
                    continue;
                }

                _db.PharmacyStockBatches.Add(new PharmacyStockBatch
                {
                    HospitalId = hospitalId,
                    BranchId = branchId,
                    MedicineId = medicine.Id,
                    BatchNumber = row.Batch,
                    Quantity = row.Quantity,
                    SellingPrice = row.SellingPrice,
                    ExpiryDate = row.ExpiryDate,
                    ManufactureDate = row.ManufactureDate,
                    SupplierCode = row.SupplierCode,
                    SupplierName = row.SupplierName,
                    PurchasePrice = row.PurchasePrice,
                    GST = row.GST,
                    MinimumStock = row.MinimumStock,
                    MaximumStock = row.MaximumStock,
                    ReorderLevel = row.ReorderLevel,
                    StorageLocation = row.StorageLocation
                });
                await _db.SaveChangesAsync();
                batchesCreated++;
            }

            await transaction.CommitAsync();
            return Ok(new
            {
                message = "Excel import completed.",
                hospitalId,
                branchId,
                totalRows = rows.Count,
                medicinesCreated,
                medicinesUpdated,
                batchesCreated,
                batchesUpdated,
                duplicatesSkipped
            });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Conflict(new { message = "Import could not be completed. Check for conflicting medicine or batch records." });
        }
        catch
        {
            await transaction.RollbackAsync();
            return StatusCode(500, "Import failed. No changes were committed.");
        }
    }

    [HttpGet]
    [Authorize(Roles = "Pharmacist,Doctor")]
    public async Task<IActionResult> GetMedicines()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            return Unauthorized();
        var currentUser = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive);
        if (currentUser == null || !currentUser.HospitalId.HasValue)
            return Forbid();

        int hospitalId = currentUser.HospitalId.Value;
        var medicines = await _db.Medicines.AsNoTracking()
            .Where(x => x.HospitalId == hospitalId && x.IsActive)
            .OrderBy(x => x.Name).ToListAsync();

        var batchesQuery = _db.PharmacyStockBatches.AsNoTracking()
            .Where(x => x.HospitalId == hospitalId);
        if (currentUser.Role == "Pharmacist")
        {
            if (!currentUser.BranchId.HasValue) return Forbid();
            int branchId = currentUser.BranchId.Value;
            batchesQuery = batchesQuery.Where(x => x.BranchId == branchId);
        }
        var batches = await batchesQuery.ToListAsync();
        var byMedicine = batches.ToLookup(x => x.MedicineId);
        var result = medicines.SelectMany(m =>
        {
            var medicineBatches = byMedicine[m.Id].ToList();
            return medicineBatches.Count == 0
                ? new[] { new { Medicine = m, Batch = (PharmacyStockBatch?)null } }
                : medicineBatches.Select(b => new { Medicine = m, Batch = (PharmacyStockBatch?)b });
        }).Select(x => new
        {
            id = x.Medicine.Id,
            medicineCode = x.Medicine.MedicineCode,
            medicineName = x.Medicine.Name,
            name = x.Medicine.Name, // Existing clients may use "name".
            genericName = x.Medicine.GenericName,
            brandName = x.Medicine.BrandName,
            category = x.Medicine.Category,
            dosageForm = x.Medicine.DosageForm,
            strength = x.Medicine.Strength,
            manufacturer = x.Medicine.Manufacturer,
            packSize = x.Medicine.PackSize,
            supplierCode = x.Batch?.SupplierCode,
            supplierName = x.Batch?.SupplierName,
            batchNumber = x.Batch?.BatchNumber,
            manufactureDate = x.Batch?.ManufactureDate,
            expiryDate = x.Batch?.ExpiryDate,
            quantity = x.Batch?.Quantity,
            purchasePrice = x.Batch?.PurchasePrice,
            sellingPrice = x.Batch?.SellingPrice,
            gst = x.Batch?.GST,
            minimumStock = x.Batch?.MinimumStock,
            maximumStock = x.Batch?.MaximumStock,
            reorderLevel = x.Batch?.ReorderLevel,
            storageLocation = x.Batch?.StorageLocation,
            batchId = x.Batch?.Id,
            branchId = x.Batch?.BranchId
        }).ToList();

        return Ok(result);
    }

    private sealed record ImportRow(
        string Code, string Name, string? GenericName, string? BrandName, string? Category,
        string? DosageForm, string? Strength, string? Manufacturer, string? PackSize,
        string? SupplierCode, string? SupplierName, string Batch, DateTime? ManufactureDate,
        DateTime ExpiryDate, int Quantity, decimal? PurchasePrice, decimal SellingPrice,
        decimal? GST, int? MinimumStock, int? MaximumStock, int? ReorderLevel,
        string? StorageLocation);
}
