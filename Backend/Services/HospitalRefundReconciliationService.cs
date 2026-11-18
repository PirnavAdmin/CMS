using System.Data;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

/// <summary>
/// Read-only gateway reconciliation for order-backed and QR-backed hospital refunds.
/// Never creates a new refund at Razorpay.
/// Invoke from an authorized hospital-admin endpoint or a controlled job.
/// </summary>
public sealed class HospitalRefundReconciliationService
{
    private readonly AppDbContext _db;
    private readonly HospitalRazorpayGatewayService _gateway;

    public HospitalRefundReconciliationService(AppDbContext db, HospitalRazorpayGatewayService gateway)
    {
        _db = db;
        _gateway = gateway;
    }

    public sealed record Result(long RefundId, string Outcome, string Message, string? GatewayRefundId = null);

    public async Task<Result> ReconcileAsync(int hospitalId, long refundId, CancellationToken cancellationToken = default)
    {
        if (hospitalId <= 0 || refundId <= 0)
            return new Result(refundId, "Invalid", "Valid hospital and refund IDs are required.");

        var refund = await _db.HospitalPaymentRefunds.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == refundId && x.HospitalId == hospitalId, cancellationToken);
        if (refund == null)
            return new Result(refundId, "NotFound", "Hospital refund not found.");

        var payment = await _db.HospitalPaymentTransactions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == refund.HospitalPaymentTransactionId && x.HospitalId == hospitalId, cancellationToken);
        if (payment == null || payment.BillingId != refund.BillingId)
            return new Result(refundId, "ReviewRequired", "Refund transaction or billing identity is inconsistent.");

        if (payment.Status != "Captured" || string.IsNullOrWhiteSpace(payment.GatewayPaymentId)
            || payment.Mode is not ("Test" or "Live"))
            return new Result(refundId, "ReviewRequired", "Original payment is not a valid captured Razorpay payment.");

        // Resolve the credential belonging to the original merchant/payment source.
        // Never fall back to another hospital's merchant credentials.
        int? credentialId;
        if (payment.PaymentSource == "Qr")
        {
            if (!payment.HospitalPaymentQrId.HasValue || payment.HospitalPaymentOrderId.HasValue
                || !string.IsNullOrWhiteSpace(payment.GatewayOrderId))
                return new Result(refundId, "ReviewRequired", "QR transaction source or references are inconsistent.");

            var qr = await _db.HospitalPaymentQrs.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == payment.HospitalPaymentQrId.Value
                    && x.HospitalId == hospitalId && x.Mode == payment.Mode, cancellationToken);
            if (qr == null || qr.BillingId != payment.BillingId
                || qr.GatewayPaymentId != payment.GatewayPaymentId
                || qr.Status != "Paid" || qr.Amount != payment.Amount)
                return new Result(refundId, "ReviewRequired", "Original QR payment identity is inconsistent.");

            credentialId = qr.RazorpayCredentialId;
        }
        else if (payment.PaymentSource == "Order")
        {
            if (!payment.HospitalPaymentOrderId.HasValue || payment.HospitalPaymentQrId.HasValue
                || string.IsNullOrWhiteSpace(payment.GatewayOrderId))
                return new Result(refundId, "ReviewRequired", "Order transaction source or references are inconsistent.");

            var order = await _db.HospitalPaymentOrders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == payment.HospitalPaymentOrderId.Value
                    && x.HospitalId == hospitalId && x.Mode == payment.Mode, cancellationToken);
            if (order == null || order.BillingId != payment.BillingId
                || order.GatewayPaymentId != payment.GatewayPaymentId
                || order.GatewayOrderId != payment.GatewayOrderId
                || order.Amount != payment.Amount || order.Status != "Captured")
                return new Result(refundId, "ReviewRequired", "Original payment order identity is inconsistent.");

            credentialId = order.RazorpayCredentialId;
        }
        else
        {
            return new Result(refundId, "ReviewRequired", "Unrecognized payment source; manual review is required.");
        }

        if (string.IsNullOrWhiteSpace(refund.GatewayRefundId))
            return new Result(refundId, "ReviewRequired", "Local gateway refund identifier is missing.");

        var isPlaceholder = refund.GatewayRefundId.StartsWith("REQUESTED-", StringComparison.OrdinalIgnoreCase);
        (string RefundId, string PaymentId, long AmountPaise, string Currency, string Status) remote;
        try
        {
            if (isPlaceholder)
            {
                var all = await _gateway.FetchPaymentRefundsAsync(
                    hospitalId, payment.Mode, payment.GatewayPaymentId, credentialId);
                var knownIds = await _db.HospitalPaymentRefunds.AsNoTracking()
                    .Where(x => x.HospitalPaymentTransactionId == payment.Id && x.Id != refundId)
                    .Select(x => x.GatewayRefundId).ToListAsync(cancellationToken);
                var unknownLocalCount = await _db.HospitalPaymentRefunds.AsNoTracking()
                    .CountAsync(x => x.HospitalPaymentTransactionId == payment.Id
                        && x.GatewayRefundId.StartsWith("REQUESTED-"), cancellationToken);

                // Without a durable provider request ID, multiple unknown local refunds are ambiguous.
                if (unknownLocalCount != 1)
                    return new Result(refundId, "ReviewRequired", "Multiple unresolved local refunds exist for this payment; manual matching is required.");

                var candidates = all.Where(x => x.PaymentId == payment.GatewayPaymentId && x.Currency == "INR"
                    && x.AmountPaise == checked((long)(refund.Amount * 100m))
                    && !knownIds.Contains(x.RefundId)).ToList();
                if (candidates.Count != 1)
                    return new Result(refundId, "ReviewRequired", $"Expected exactly one unmatched gateway refund; found {candidates.Count}.");
                remote = candidates[0];
            }
            else
            {
                remote = await _gateway.FetchRefundAsync(
                    hospitalId, payment.Mode, refund.GatewayRefundId, credentialId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // A gateway lookup failure must not alter the local refund or trigger a new refund.
            return new Result(refundId, "GatewayUnavailable", "Gateway lookup failed. No refund was issued or changed.");
        }

        if (string.IsNullOrWhiteSpace(remote.RefundId) || remote.PaymentId != payment.GatewayPaymentId
            || remote.Currency != "INR" || remote.AmountPaise != checked((long)(refund.Amount * 100m)))
            return new Result(refundId, "ReviewRequired", "Gateway refund identity, currency, or amount does not match.");

        var status = remote.Status.ToLowerInvariant();
        if (status is not ("processed" or "failed" or "pending" or "created"))
            return new Result(refundId, "ReviewRequired", $"Unrecognized gateway refund status: {remote.Status}.");

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // FOR UPDATE serializes concurrent reconciliation of this local refund in MySQL.
        var current = await _db.HospitalPaymentRefunds.FromSqlInterpolated(
            $"SELECT * FROM HospitalPaymentRefunds WHERE Id = {refundId} AND HospitalId = {hospitalId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (current == null || current.HospitalPaymentTransactionId != payment.Id || current.Amount != refund.Amount
            || current.GatewayRefundId != refund.GatewayRefundId)
            return new Result(refundId, "ReviewRequired", "Local refund changed during reconciliation; retry review.");

        var otherOwner = await _db.HospitalPaymentRefunds.AsNoTracking().AnyAsync(
            x => x.GatewayRefundId == remote.RefundId && x.Id != refundId, cancellationToken);
        if (otherOwner)
            return new Result(refundId, "ReviewRequired", "Gateway refund ID is already assigned to another local refund.");

        // Never regress a processed refund based on a late or out-of-order notification.
        if (current.Status.Equals("processed", StringComparison.OrdinalIgnoreCase) && status != "processed")
            return new Result(refundId, "ReviewRequired", "Local refund is already processed; conflicting gateway status needs review.");

        current.GatewayRefundId = remote.RefundId;
        current.Status = status;
        if (status == "processed") current.CompletedAt ??= DateTime.UtcNow;
        else current.CompletedAt = null;

        // Flush status first so subsequent aggregate queries see this refund.
        await _db.SaveChangesAsync(cancellationToken);
        var trackedPayment = await _db.HospitalPaymentTransactions.SingleAsync(
            x => x.Id == payment.Id && x.HospitalId == hospitalId, cancellationToken);
        trackedPayment.RefundedAmount = await _db.HospitalPaymentRefunds
            .Where(x => x.HospitalPaymentTransactionId == payment.Id && x.HospitalId == hospitalId && x.Status == "processed")
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var bill = await _db.Billings.SingleOrDefaultAsync(
            x => x.Id == payment.BillingId && x.HospitalId == hospitalId, cancellationToken);
        if (bill != null)
        {
            var paid = await _db.BillingPaymentTransactions
                .Where(x => x.BillingId == bill.Id && x.HospitalId == hospitalId && x.Status == "Paid")
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
            var refunds = await _db.HospitalPaymentRefunds
                .Where(x => x.BillingId == bill.Id && x.HospitalId == hospitalId && x.Status == "processed")
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
            var lostDisputes = await _db.HospitalPaymentDisputes
                .Where(x => x.HospitalId == hospitalId && x.Transaction.BillingId == bill.Id && x.Status == "Lost")
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
            var netPaid = Math.Max(0m, paid - refunds - lostDisputes);
            bill.Status = netPaid >= bill.TotalAmount ? "Paid" : netPaid > 0m ? "PartiallyPaid" : "Pending";
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new Result(refundId, "Reconciled", $"Refund reconciled with gateway status '{status}'.", remote.RefundId);
    }
}
