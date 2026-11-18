
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public sealed class HospitalSettlementRecord
{
    // ==========================================
    // EXISTING PROPERTIES
    // ==========================================

    public long Id { get; set; }

    public int HospitalId { get; set; }

    public long HospitalPaymentTransactionId { get; set; }

    public HospitalPaymentTransaction Transaction { get; set; } = null!;

    [Required, MaxLength(150)]
    public string SettlementId { get; set; } = "";

    public decimal GrossAmount { get; set; }

    public decimal FeeAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal NetAmount { get; set; }

    public DateTime SettledAt { get; set; }

    // Changed default from Matched to Pending.
    // Matching must be verified using Razorpay evidence.
    [Required, MaxLength(30)]
    public string ReconciliationStatus { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ==========================================
    // CHANGE #19: SETTLEMENT RECONCILIATION
    // ==========================================

    // Test or Live Razorpay environment.
    [Required, MaxLength(20)]
    public string Mode { get; set; } = "Test";

    // Gateway payment ID associated with this record.
    [MaxLength(150)]
    public string? GatewayPaymentId { get; set; }

    // Gateway-reported settlement transaction ID,
    // if provided by Razorpay.
    [MaxLength(150)]
    public string? GatewaySettlementTransactionId { get; set; }

    // Provider-reported transaction type.
    // Example: payment or refund.
    [MaxLength(30)]
    public string? GatewayTransactionType { get; set; }

    // Currency returned by the gateway.
    [Required, MaxLength(3)]
    public string Currency { get; set; } = "INR";

    // Expected payment amount from the local transaction.
    public decimal? ExpectedGrossAmount { get; set; }

    // Expected net amount when the relevant fee and
    // tax evidence is available.
    public decimal? ExpectedNetAmount { get; set; }

    // Difference between gateway and expected gross.
    public decimal? GrossDifference { get; set; }

    // Difference between gateway and expected net.
    public decimal? NetDifference { get; set; }

    // Date and time of the last reconciliation attempt.
    public DateTime? LastReconciliationAttemptAtUtc { get; set; }

    // Date and time when matching was completed.
    public DateTime? ReconciledAtUtc { get; set; }

    // Number of reconciliation attempts.
    public int ReconciliationAttemptCount { get; set; } = 0;

    // Reason for a mismatch or manual review.
    [MaxLength(1000)]
    public string? ReconciliationNotes { get; set; }

    // Indicates that an unresolved discrepancy
    // requires an administrator's attention.
    public bool RequiresManualReview { get; set; } = false;

    // Timestamp of the latest local update.
    public DateTime? UpdatedAtUtc { get; set; }
}
