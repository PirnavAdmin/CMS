
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public sealed class HospitalPaymentOrder
{
    public long Id { get; set; }

    [Required, MaxLength(80)]
    public string LocalOrderId { get; set; } =
        Guid.NewGuid().ToString("N");

    public int HospitalId { get; set; }

    public int BillingId { get; set; }

    public int PatientId { get; set; }

    public int? BranchId { get; set; }

    public decimal Amount { get; set; }

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "INR";

    [Required, MaxLength(20)]
    public string Mode { get; set; } = "Test";

    [MaxLength(150)]
    public string? GatewayOrderId { get; set; }

    [MaxLength(150)]
    public string? GatewayPaymentId { get; set; }

    // Identifies the credential version used to
    // create this Razorpay order.
    // Null supports older orders and legacy configuration.
    public int? RazorpayCredentialId { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Pending";

    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
