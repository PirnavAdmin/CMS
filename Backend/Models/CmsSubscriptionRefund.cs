
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public sealed class CmsSubscriptionRefund
{
    public long Id { get; set; }

    public int SubscriptionPurchaseId { get; set; }

    public int? HospitalId { get; set; }

    // Razorpay refund ID.
    // Empty until Razorpay returns the refund reference.
    [Required, MaxLength(150)]
    public string GatewayRefundId { get; set; } = "";

    // Original Razorpay payment being refunded.
    [MaxLength(150)]
    public string? GatewayPaymentId { get; set; }

    // Stable request key for retry and duplicate protection.
    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    public decimal Amount { get; set; }

    [Required, MaxLength(20)]
    public string Mode { get; set; } = "Test";

    [Required, MaxLength(30)]
    public string Status { get; set; } = "requested";

    [MaxLength(300)]
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}
