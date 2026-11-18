
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

// =====================================================
// POST - CREATE MEDICINE WITH OPTIONAL STOCK BATCH
// =====================================================

public class CreateMedicineCrudDto
{
    // MEDICINE MASTER

    [Required]
    [MaxLength(50)]
    public string MedicineCode { get; set; } = "";

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(200)]
    public string? GenericName { get; set; }

    [MaxLength(200)]
    public string? BrandName { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(100)]
    public string? DosageForm { get; set; }

    [MaxLength(100)]
    public string? Strength { get; set; }

    [MaxLength(200)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? PackSize { get; set; }

    public bool RequiresPrescription { get; set; } = true;

    // BRANCH AND BATCH

    [Range(1, int.MaxValue)]
    public int? BranchId { get; set; }

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    public DateTime? ManufactureDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [Range(0, int.MaxValue)]
    public int? StockQuantity { get; set; }

    // SUPPLIER

    [MaxLength(50)]
    public string? SupplierCode { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    // PRICING

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal? PurchasePrice { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal? SellingPrice { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal? GST { get; set; }

    // STOCK LIMITS AND STORAGE

    [Range(0, int.MaxValue)]
    public int? MinimumStock { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaximumStock { get; set; }

    [Range(0, int.MaxValue)]
    public int? ReorderLevel { get; set; }

    [MaxLength(200)]
    public string? StorageLocation { get; set; }
}

// =====================================================
// PUT - FULL MEDICINE AND BATCH UPDATE
// =====================================================

// Inherits all POST fields so PUT can receive
// the complete frontend Edit Medicine form.

public class UpdateMedicineCrudDto : CreateMedicineCrudDto
{
    // Identifies the existing stock batch.
    // Required by controller logic when editing batch fields.

    [Range(1, int.MaxValue)]
    public int? BatchId { get; set; }
}

// =====================================================
// GET - MEDICINE MASTER RESPONSE
// =====================================================

public class MedicineCrudResponseDto
{
    public int Id { get; set; }

    public string MedicineCode { get; set; } = "";

    public string Name { get; set; } = "";

    public string? GenericName { get; set; }

    public string? BrandName { get; set; }

    public string? Category { get; set; }

    public string? DosageForm { get; set; }

    public string? Strength { get; set; }

    public string? Manufacturer { get; set; }

    public string? PackSize { get; set; }

    public bool RequiresPrescription { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeactivatedAt { get; set; }

    public DateTime? ReactivatedAt { get; set; }
}

// =====================================================
// GET - STOCK BATCH RESPONSE
// =====================================================

// Excludes supplier purchase costs from the
// general response to protect sensitive pricing.

public class MedicineCrudBatchResponseDto
{
    public int Id { get; set; }

    public int MedicineId { get; set; }

    public int BranchId { get; set; }

    public string BatchNumber { get; set; } = "";

    public DateTime? ManufactureDate { get; set; }

    public DateTime ExpiryDate { get; set; }

    public int StockQuantity { get; set; }

    public decimal SellingPrice { get; set; }

    public decimal? GST { get; set; }

    public int? MinimumStock { get; set; }

    public int? MaximumStock { get; set; }

    public int? ReorderLevel { get; set; }

    public string? StorageLocation { get; set; }
}
