using System;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

// =====================================================
// PAYMENTS - CMS RAZORPAY PAYMENT RECONCILIATION
//
// Uses CMS Owner credentials only.
// Hospital merchant credentials are never used here.
// This service verifies gateway evidence; it does not
// mutate or activate subscription purchases.
// =====================================================

public sealed class CmsRazorpayPaymentReconciliationService
{
    private readonly AppDbContext _db;
    private readonly CmsSubscriptionRazorpayService _gateway;

    public CmsRazorpayPaymentReconciliationService(
        AppDbContext db,
        CmsSubscriptionRazorpayService gateway)
    {
        _db = db;
        _gateway = gateway;
    }

    public async Task<PaymentVerificationResult> VerifyAsync(
        int purchaseId,
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        if (purchaseId <= 0 || string.IsNullOrWhiteSpace(paymentId))
            return new(false, "Invalid purchase or payment ID.");

        // Do not silently normalize a gateway identifier: the caller must
        // supply the exact ID returned by Razorpay.
        if (paymentId != paymentId.Trim() || paymentId.Length > 150)
            return new(false, "Invalid Razorpay payment ID format.");

        var purchase = await _db.SubscriptionPurchases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == purchaseId, cancellationToken);

        if (purchase == null)
            return new(false, "Purchase not found.");

        if (string.IsNullOrWhiteSpace(purchase.GatewayOrderId))
            return new(false, "Gateway order is missing.");

        var mode = purchase.GatewayMode;
        if (mode is not ("Test" or "Live"))
            return new(false, "Original gateway mode is missing.");

        if (!string.IsNullOrEmpty(purchase.GatewayPaymentId) &&
            !string.Equals(purchase.GatewayPaymentId, paymentId, StringComparison.Ordinal))
            return new(false, "A different payment is recorded.");

        // A single Razorpay payment must not be associated with two CMS
        // subscription purchases. Database uniqueness is still required
        // to enforce this invariant across concurrent writers.
        var usedElsewhere = await _db.SubscriptionPurchases
            .AsNoTracking()
            .AnyAsync(x => x.Id != purchaseId &&
                           x.GatewayPaymentId == paymentId,
                      cancellationToken);

        if (usedElsewhere)
            return new(false, "Payment is already associated with another purchase.");

        cancellationToken.ThrowIfCancellationRequested();

        // Verify using the original purchase's mode and credential version.
        // Historical NULL credential IDs require separate operational review.
        var captured = await _gateway.IsCapturedPaymentAsync(
            paymentId,
            purchase.GatewayOrderId,
            purchase.Amount,
            mode,
            purchase.RazorpayCredentialId);

        cancellationToken.ThrowIfCancellationRequested();

        if (!captured)
            return new(false, "Razorpay could not confirm a matching captured payment.");

        return new(true, "Captured Razorpay payment verified successfully.");
    }
}

public sealed record PaymentVerificationResult(
    bool Verified,
    string Message);
