using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Models;

public class PharmacyStockBatch
{
    public int Id { get; set; }

    public int HospitalId { get; set; }
    public int BranchId { get; set; }

    public int MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;

    [Required, MaxLength(100)]
    public string BatchNumber { get; set; } = "";

    public int Quantity { get; set; }

    [Precision(18, 2)]
    public decimal SellingPrice { get; set; }

    public DateTime ExpiryDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Optional additional Excel import fields.
    [MaxLength(50)]
    public string? SupplierCode { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    public DateTime? ManufactureDate { get; set; }

    [Precision(18, 2)]
    public decimal? PurchasePrice { get; set; }

    // GST percentage, e.g. 5, 12, or 18.
    [Precision(5, 2)]
    public decimal? GST { get; set; }

    public int? MinimumStock { get; set; }
    public int? MaximumStock { get; set; }
    public int? ReorderLevel { get; set; }

    [MaxLength(200)]
    public string? StorageLocation { get; set; }
}
