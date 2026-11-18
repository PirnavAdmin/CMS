using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Services;

/// <summary>
/// Reconciles one already-captured Razorpay payment against a hospital invoice.
/// Does not create, capture, or refund a payment.
/// The caller must authorize hospital access; the background worker is trusted.
/// </summary>
public sealed class HospitalPaymentRecoveryExecutor
{
    private readonly AppDbContext _db;
    private readonly HospitalRazorpayGatewayService _gateway;
    private readonly HospitalPaymentAllocationService _allocation;
    private readonly HospitalPaymentRecoveryService _attempts;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HospitalPaymentRecoveryExecutor> _logger;

    public HospitalPaymentRecoveryExecutor(
        AppDbContext db,
        HospitalRazorpayGatewayService gateway,
        HospitalPaymentAllocationService allocation,
        HospitalPaymentRecoveryService attempts,
        IConfiguration configuration,
        ILogger<HospitalPaymentRecoveryExecutor> logger)
    {
        _db = db;
        _gateway = gateway;
        _allocation = allocation;
        _attempts = attempts;
        _configuration = configuration;
        _logger = logger;
    }

    public sealed record RecoveryResult(
        string Status,
        string Message,
        long? AttemptId = null);

    private async Task<RecoveryResult?> CheckExistingAllocationAsync(
        int hospitalId,
        string paymentSource,
        string gatewayPaymentId,
        long? orderId,
        long? qrId,
        CancellationToken cancellationToken)
    {
        var capture = await _db.HospitalPaymentTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.GatewayPaymentId == gatewayPaymentId,
                cancellationToken);

        // Check the billing ledger independently: an orphan billing entry is a
        // conflict, not permission to insert a second payment allocation.
        var ledger = await _db.BillingPaymentTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.PaymentMethod == "Razorpay" &&
                     x.ReferenceNumber == gatewayPaymentId,
                cancellationToken);

        if (capture == null && ledger == null)
            return null;

        if (capture == null || ledger == null)
            return new RecoveryResult(
                "ReviewRequired",
                "Only one payment ledger contains this gateway payment. Manual review required.");

        bool sameSource =
            capture.HospitalId == hospitalId &&
            capture.PaymentSource == paymentSource &&
            capture.HospitalPaymentOrderId == orderId &&
            capture.HospitalPaymentQrId == qrId &&
            capture.Status == "Captured" &&
            capture.BillingPaymentTransactionId == ledger.Id &&
            ledger.HospitalId == hospitalId &&
            ledger.BillingId == capture.BillingId &&
            ledger.PaymentMethod == "Razorpay" &&
            ledger.ReferenceNumber == gatewayPaymentId &&
            ledger.Status == "Paid" &&
            ledger.Amount == capture.Amount;

        if (!sameSource)
            return new RecoveryResult(
                "ReviewRequired",
                "Existing payment ledger conflicts with the requested hospital, source, or amount.");

        // Check the source row too, not just the two ledgers.
        if (paymentSource == "Order")
        {
            var order = await _db.HospitalPaymentOrders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == orderId!.Value &&
                                           x.HospitalId == hospitalId,
                    cancellationToken);

            if (order == null || order.Status != "Captured" ||
                order.GatewayPaymentId != gatewayPaymentId ||
                order.BillingId != capture.BillingId ||
                order.GatewayOrderId != capture.GatewayOrderId ||
                order.Mode != capture.Mode ||
                order.Amount != capture.Amount)
                return new RecoveryResult(
                    "ReviewRequired",
                    "Order state does not match the recorded capture.");
        }
        else
        {
            var qr = await _db.HospitalPaymentQrs.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == qrId!.Value &&
                                           x.HospitalId == hospitalId,
                    cancellationToken);

            if (qr == null || qr.Status != "Paid" ||
                qr.GatewayPaymentId != gatewayPaymentId ||
                qr.BillingId != capture.BillingId ||
                qr.Mode != capture.Mode ||
                qr.Amount != capture.Amount)
                return new RecoveryResult(
                    "ReviewRequired",
                    "QR state does not match the recorded capture.");
        }

        return new RecoveryResult(
            "AlreadyAllocated",
            "Payment is already recorded consistently in both ledgers.");
    }

    public async Task<RecoveryResult> ExecuteAsync(
        int hospitalId,
        string paymentSource,
        string gatewayPaymentId,
        long? orderId = null,
        long? qrId = null,
        CancellationToken cancellationToken = default)
    {
        if (hospitalId <= 0 ||
            string.IsNullOrWhiteSpace(gatewayPaymentId) ||
            gatewayPaymentId.Trim().Length > 150 ||
            (paymentSource != "Order" && paymentSource != "Qr") ||
            (paymentSource == "Order" &&
                (!orderId.HasValue || orderId.Value <= 0 || qrId.HasValue)) ||
            (paymentSource == "Qr" &&
                (!qrId.HasValue || qrId.Value <= 0 || orderId.HasValue)))
            return new("ReviewRequired", "Invalid hospital, payment source, or gateway payment ID.");

        if (!_configuration.GetValue<bool>(
                "HospitalPaymentRecovery:AutomaticAllocationEnabled"))
            return new("Disabled", "Automatic payment allocation is disabled.");

        cancellationToken.ThrowIfCancellationRequested();
        gatewayPaymentId = gatewayPaymentId.Trim();

        var existing = await CheckExistingAllocationAsync(
            hospitalId, paymentSource, gatewayPaymentId,
            orderId, qrId, cancellationToken);
        if (existing != null)
            return existing;

        string mode;
        string gatewaySourceId;
        decimal amount;
        int? credentialId;

        if (paymentSource == "Order")
        {
            var order = await _db.HospitalPaymentOrders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == orderId!.Value &&
                                           x.HospitalId == hospitalId,
                    cancellationToken);

            if (order == null || order.Mode is not ("Test" or "Live") ||
                string.IsNullOrWhiteSpace(order.GatewayOrderId) ||
                order.Amount <= 0 || order.Currency != "INR" ||
                order.Status == "Captured" ||
                (!string.IsNullOrWhiteSpace(order.GatewayPaymentId) &&
                 order.GatewayPaymentId != gatewayPaymentId))
                return new("ReviewRequired", "Order identity or state is inconsistent.");

            mode = order.Mode;
            gatewaySourceId = order.GatewayOrderId;
            amount = order.Amount;
            credentialId = order.RazorpayCredentialId;
        }
        else
        {
            var qr = await _db.HospitalPaymentQrs.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == qrId!.Value &&
                                           x.HospitalId == hospitalId,
                    cancellationToken);

            if (qr == null || qr.Mode is not ("Test" or "Live") ||
                string.IsNullOrWhiteSpace(qr.GatewayQrId) ||
                qr.Amount <= 0 || qr.Currency != "INR" ||
                (qr.Status != "Active" && qr.Status != "Creating") ||
                (!string.IsNullOrWhiteSpace(qr.GatewayPaymentId) &&
                 qr.GatewayPaymentId != gatewayPaymentId))
                return new("ReviewRequired", "QR identity or state is inconsistent.");

            mode = qr.Mode;
            gatewaySourceId = qr.GatewayQrId;
            amount = qr.Amount;
            credentialId = qr.RazorpayCredentialId;
        }

        // Fail closed for legacy rows without a pinned merchant credential.
        // They require manual reconciliation against the original merchant.
        if (!credentialId.HasValue || credentialId.Value <= 0)
            return new("ReviewRequired", "Original Razorpay merchant credential is not pinned.");

        HospitalPaymentRecoveryAttempt? attempt;
        try
        {
            attempt = await _attempts.TryStartAttemptAsync(
                hospitalId, paymentSource, gatewayPaymentId,
                orderId, qrId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex,
                "Recovery attempt not eligible. HospitalId={HospitalId}, Source={Source}",
                hospitalId, paymentSource);
            return new("ReviewRequired", "Recovery attempt is not eligible; inspect attempt history.");
        }

        if (attempt == null)
            return new("Busy", "Another process is starting recovery for this payment.");

        try
        {
            bool verified;
            if (paymentSource == "Order")
            {
                // The gateway service uses the hospital's pinned credential.
                // Never contact the gateway inside an EF transaction.
                var remote = await _gateway.FetchPaymentAsync(
                    hospitalId, mode, gatewayPaymentId, credentialId);

                verified = remote.Captured &&
                           remote.OrderId == gatewaySourceId &&
                           remote.Currency == "INR" &&
                           remote.AmountPaise == checked((long)(amount * 100m));
            }
            else
            {
                verified = await _gateway.VerifyCapturedQrPaymentAsync(
                    hospitalId, mode, gatewaySourceId,
                    gatewayPaymentId, amount, credentialId);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!verified)
            {
                await _attempts.CompleteAttemptAsync(
                    attempt.Id, "ManualReview",
                    "Gateway capture, merchant, amount, or source identity could not be verified.",
                    cancellationToken);
                return new("ReviewRequired", "Razorpay verification did not match.", attempt.Id);
            }

            // Webhooks may have completed the same payment while the remote
            // verification was in flight. Do not allocate if either ledger
            // now contains the gateway payment ID.
            var afterVerification = await CheckExistingAllocationAsync(
                hospitalId, paymentSource, gatewayPaymentId,
                orderId, qrId, cancellationToken);

            if (afterVerification != null)
            {
                if (afterVerification.Status == "AlreadyAllocated")
                {
                    await _attempts.CompleteAttemptAsync(
                        attempt.Id, "Succeeded", cancellationToken: cancellationToken);
                    return new("AlreadyAllocated",
                        "Payment was allocated by another verified process.", attempt.Id);
                }

                await _attempts.CompleteAttemptAsync(
                    attempt.Id, "ManualReview", afterVerification.Message,
                    cancellationToken);
                return new("ReviewRequired", afterVerification.Message, attempt.Id);
            }

            bool allocated = paymentSource == "Order"
                ? await _allocation.AllocateOrderAsync(
                    hospitalId, orderId!.Value, gatewayPaymentId, cancellationToken)
                : await _allocation.AllocateQrAsync(
                    hospitalId, qrId!.Value, gatewayPaymentId, cancellationToken);

            if (!allocated)
            {
                // Re-check: a webhook may have won the race and committed.
                var raced = await CheckExistingAllocationAsync(
                    hospitalId, paymentSource, gatewayPaymentId,
                    orderId, qrId, cancellationToken);

                if (raced?.Status == "AlreadyAllocated")
                {
                    await _attempts.CompleteAttemptAsync(
                        attempt.Id, "Succeeded", cancellationToken: cancellationToken);
                    return new("AlreadyAllocated",
                        "Payment was allocated concurrently.", attempt.Id);
                }

                await _attempts.CompleteAttemptAsync(
                    attempt.Id, "ManualReview",
                    "Verified payment could not be allocated safely. Inspect invoice and both ledgers.",
                    cancellationToken);
                return new("ReviewRequired",
                    "Verified payment requires manual reconciliation.", attempt.Id);
            }

            await _attempts.CompleteAttemptAsync(
                attempt.Id, "Succeeded", cancellationToken: cancellationToken);
            return new("Succeeded", "Verified captured payment allocated.", attempt.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Outcome could be uncertain if cancellation occurred during a commit.
            // Stale-attempt monitoring will flag this for manual review.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Payment recovery failed. HospitalId={HospitalId}, AttemptId={AttemptId}",
                hospitalId, attempt.Id);

            // A database commit might have succeeded before the exception.
            // Do not automatically retry an uncertain outcome.
            try
            {
                await _attempts.CompleteAttemptAsync(
                    attempt.Id, "ManualReview",
                    "Unexpected recovery error. Inspect gateway and both ledgers before retry.",
                    CancellationToken.None);
            }
            catch (Exception completionEx)
            {
                _logger.LogError(completionEx,
                    "Could not mark recovery attempt {AttemptId} for manual review",
                    attempt.Id);
            }

            return new("ReviewRequired",
                "Recovery outcome uncertain; manual review required.", attempt.Id);
        }
    }
}
