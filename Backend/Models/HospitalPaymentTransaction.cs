
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public sealed class HospitalPaymentTransaction
{
    public long Id { get; set; }

    // Standard checkout payments reference an order.
    // QR payments reference a QR.
    public long? HospitalPaymentOrderId { get; set; }
    public long? HospitalPaymentQrId { get; set; }

    public HospitalPaymentOrder? Order { get; set; }
    public HospitalPaymentQr? Qr { get; set; }

    public int HospitalId { get; set; }
    public int BillingId { get; set; }
    public int BillingPaymentTransactionId { get; set; }

    [Required, MaxLength(150)]
    public string GatewayPaymentId { get; set; } = "";

    // QR payments may not have a Razorpay order ID.
    [MaxLength(150)]
    public string? GatewayOrderId { get; set; }

    // "Order" or "Qr".
    [Required, MaxLength(10)]
    public string PaymentSource { get; set; } = "Order";

    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }

    [Required, MaxLength(20)]
    public string Mode { get; set; } = "Test";

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Captured";

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    // ------------------------------------------
    // Settlement reconciliation fields
    // ------------------------------------------

    // Razorpay settlement identifier.
    // Nullable until gateway settlement is confirmed.
    [MaxLength(150)]
    public string? GatewaySettlementId { get; set; }

    // Provider-reported fee and tax for this payment.
    public decimal? SettlementFeeAmount { get; set; }
    public decimal? SettlementTaxAmount { get; set; }

    // Amount credited after applicable deductions.
    public decimal? SettlementNetAmount { get; set; }

    // Pending, Matched, Mismatch, ManualReview.
    [Required, MaxLength(30)]
    public string SettlementReconciliationStatus { get; set; }
        = "Pending";

    // Timestamp when settlement reconciliation completed.
    public DateTime? SettlementReconciledAtUtc { get; set; }

    // Timestamp reported by the gateway.
    public DateTime? GatewaySettledAtUtc { get; set; }

    // Explanation of mismatches or missing gateway records.
    [MaxLength(1000)]
    public string? SettlementReconciliationNotes { get; set; }
}
