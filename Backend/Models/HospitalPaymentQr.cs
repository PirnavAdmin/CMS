using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

/// <summary>
/// A hospital-staff-created fixed-amount Razorpay QR payment request.
/// Creating a QR is not evidence that a payment has been received.
/// </summary>
public sealed class HospitalPaymentQr
{
    public long Id { get; set; }

    [Required, MaxLength(80)]
    public string LocalReference { get; set; } = Guid.NewGuid().ToString("N");

    public int HospitalId { get; set; }
    public int BillingId { get; set; }
    public int? BranchId { get; set; }

    public decimal Amount { get; set; }

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "INR";

    [Required, MaxLength(20)]
    public string Mode { get; set; } = "Test";

    public int? RazorpayCredentialId { get; set; }

    [MaxLength(150)]
    public string? GatewayQrId { get; set; }

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    [MaxLength(150)]
    public string? GatewayPaymentId { get; set; }

    // Creating, Active, Paid, Closed, Expired, Failed.
    [Required, MaxLength(30)]
    public string Status { get; set; } = "Creating";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public int? CreatedByUserId { get; set; }
}
