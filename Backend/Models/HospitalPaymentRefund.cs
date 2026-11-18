
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public sealed class HospitalPaymentRefund
{
    public long Id { get; set; }

    public long HospitalPaymentTransactionId { get; set; }

    public HospitalPaymentTransaction Transaction { get; set; } = null!;

    public int HospitalId { get; set; }

    public int BillingId { get; set; }

    [Required, MaxLength(150)]
    public string GatewayRefundId { get; set; } = "";

    public decimal Amount { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Requested";

    [MaxLength(300)]
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    // ------------------------------------------
    // Refund settlement reconciliation
    // ------------------------------------------

    // Razorpay settlement associated with this refund,
    // when confirmed by the gateway.
    [MaxLength(150)]
    public string? GatewaySettlementId { get; set; }

    // Provider-reported settlement adjustment for this
    // refund. Nullable until the gateway data is available.
    public decimal? SettlementAdjustmentAmount { get; set; }

    // Provider-reported fees and taxes, if applicable.
    public decimal? SettlementFeeAmount { get; set; }

    public decimal? SettlementTaxAmount { get; set; }

    // Pending, Matched, Mismatch, ManualReview.
    [Required, MaxLength(30)]
    public string SettlementReconciliationStatus { get; set; }
        = "Pending";

    // Date and time reconciliation was completed.
    public DateTime? SettlementReconciledAtUtc { get; set; }

    // Settlement date reported by Razorpay.
    public DateTime? GatewaySettledAtUtc { get; set; }

    // Reason for a mismatch or manual review.
    [MaxLength(1000)]
    public string? SettlementReconciliationNotes { get; set; }
}
