
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public sealed class HospitalGatewaySettlement
{
    // ==========================================
    // EXISTING SETTLEMENT INFORMATION
    // ==========================================

    public long Id { get; set; }

    public int HospitalId { get; set; }

    [Required, MaxLength(20)]
    public string Mode { get; set; } = "Test";

    [Required, MaxLength(150)]
    public string SettlementId { get; set; } = "";

    [Required, MaxLength(30)]
    public string Status { get; set; } = "created";

    public decimal Amount { get; set; }

    public decimal FeeAmount { get; set; }

    public decimal TaxAmount { get; set; }

    [MaxLength(150)]
    public string? Utr { get; set; }

    public DateTime SettledAt { get; set; }

    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    // ==========================================
    // SETTLEMENT RECONCILIATION
    // ==========================================

    [Required, MaxLength(30)]
    public string ReconciliationStatus { get; set; } = "Pending";

    public DateTime? ReconciledAtUtc { get; set; }

    public decimal MatchedPaymentAmount { get; set; }

    public decimal MatchedRefundAmount { get; set; }

    public decimal ExpectedNetAmount { get; set; }

    public decimal SettlementDifference { get; set; }

    public int MatchedPaymentCount { get; set; }

    public int MatchedRefundCount { get; set; }

    public int UnmatchedTransactionCount { get; set; }

    [MaxLength(1000)]
    public string? ReconciliationNotes { get; set; }

    // ==========================================
    // CHANGE #19: ADDITIONAL SAFEGUARDS
    // ==========================================

    // Time of the latest reconciliation attempt.
    public DateTime? LastReconciliationAttemptAtUtc { get; set; }

    // Number of reconciliation attempts.
    public int ReconciliationAttemptCount { get; set; } = 0;

    // Flags a settlement requiring administrator review.
    public bool RequiresManualReview { get; set; } = false;

    // Number of provider transactions examined.
    public int GatewayTransactionCount { get; set; } = 0;

    // Number of provider transactions not matched
    // to an eligible local payment or refund.
    public int UnmatchedGatewayTransactionCount { get; set; } = 0;

    // Number of local transactions identified as
    // potentially belonging to this settlement but
    // not confirmed against gateway evidence.
    public int UnmatchedLocalTransactionCount { get; set; } = 0;

    // Timestamp of the latest successful retrieval
    // of complete settlement transaction evidence.
    public DateTime? GatewayEvidenceFetchedAtUtc { get; set; }

    // Tracks when reconciliation information changes.
    public DateTime? UpdatedAtUtc { get; set; }
}
