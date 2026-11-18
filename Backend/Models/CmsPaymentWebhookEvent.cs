
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

// =====================================================
// PAYMENTS - CMS RAZORPAY WEBHOOK EVENT INBOX
// Stores authenticated gateway events for safe processing.
// =====================================================

public sealed class CmsPaymentWebhookEvent
{
    public long Id { get; set; }

    [Required, MaxLength(150)]
    public string EventId { get; set; } = "";

    [Required, MaxLength(100)]
    public string EventType { get; set; } = "";

    [Required, MaxLength(20)]
    public string Mode { get; set; } = "Test";

    [MaxLength(150)]
    public string? GatewayOrderId { get; set; }

    [MaxLength(150)]
    public string? GatewayPaymentId { get; set; }

    public int? SubscriptionPurchaseId { get; set; }

    [Required, MaxLength(30)]
    public string ProcessingStatus { get; set; } = "Received";

    public int AttemptCount { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    // =====================================================
    // PAYMENTS - ORIGINAL WEBHOOK PAYLOAD
    // Needed for durable reconciliation after a restart.
    // Contains payment metadata; restrict database access.
    // =====================================================

    [Required]
    public string PayloadJson { get; set; } = "";
}
