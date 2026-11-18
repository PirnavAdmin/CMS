
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public class Medicine
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string MedicineCode { get; set; } = "";

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    // Medicine master information (shared across batches).
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

    // CMS hospital isolation.
    public int HospitalId { get; set; }

    // Default to prescription-required for safety.
    // Non-prescription classification requires authorized review.
    public bool RequiresPrescription { get; set; } = true;

    // Soft delete status.
    public bool IsActive { get; set; } = true;

    // CRUD audit information.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeactivatedAt { get; set; }

    public DateTime? ReactivatedAt { get; set; }
}
