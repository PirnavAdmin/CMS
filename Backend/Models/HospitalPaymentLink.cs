
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public class HospitalPaymentLink
{
    public long Id { get; set; }

    // Hospital that owns this payment link
    public int HospitalId { get; set; }

    // Invoice to be paid
    public int BillingId { get; set; }

    // SHA-256 hash of the public link token
    // Never store the original token
    [Required]
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    // When the link was created
    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    // Link expiration
    public DateTime ExpiresAtUtc { get; set; }

    // Allows staff to disable the link
    public DateTime? RevokedAtUtc { get; set; }

    // Staff member who created the link
    public int? CreatedByUserId { get; set; }

    // Navigation property
    public Billing Billing { get; set; } = null!;

    // Computed property, not stored in DB
    public bool IsActive =>
        RevokedAtUtc == null &&
        ExpiresAtUtc > DateTime.UtcNow;
}
