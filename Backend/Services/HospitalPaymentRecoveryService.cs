
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
/// Tracks and controls hospital payment recovery attempts.
/// Does not create Razorpay payments, capture funds,
/// issue refunds, or allocate invoice payments.
/// </summary>
public sealed class HospitalPaymentRecoveryService
{
    private readonly AppDbContext _db;
    private readonly HospitalPaymentRecoveryLockService _lockService;
    private readonly ILogger<HospitalPaymentRecoveryService> _logger;

    private readonly int _maxAttempts;
    private readonly int _initialRetryDelayMinutes;

    public HospitalPaymentRecoveryService(
        AppDbContext db,
        HospitalPaymentRecoveryLockService lockService,
        IConfiguration configuration,
        ILogger<HospitalPaymentRecoveryService> logger)
    {
        _db = db;
        _lockService = lockService;
        _logger = logger;

        _maxAttempts = Math.Clamp(
            configuration.GetValue<int?>(
                "HospitalPaymentRecovery:MaximumAttempts") ?? 5,
            1,
            10);

        _initialRetryDelayMinutes = Math.Clamp(
            configuration.GetValue<int?>(
                "HospitalPaymentRecovery:InitialRetryDelayMinutes") ?? 5,
            1,
            60);
    }

    /// <summary>
    /// Starts a recovery attempt under the existing
    /// MySQL named lock.
    ///
    /// Returns null when another process owns the lock.
    /// </summary>
    public async Task<HospitalPaymentRecoveryAttempt?>
        TryStartAttemptAsync(
            int hospitalId,
            string paymentSource,
            string gatewayPaymentId,
            long? orderId = null,
            long? qrId = null,
            CancellationToken cancellationToken = default)
    {
        ValidateRequest(
            hospitalId,
            paymentSource,
            gatewayPaymentId,
            orderId,
            qrId);

        gatewayPaymentId = gatewayPaymentId.Trim();

        await using var paymentLock =
            await _lockService.TryAcquireAsync(
                hospitalId,
                gatewayPaymentId,
                cancellationToken);

        if (paymentLock == null)
        {
            _logger.LogInformation(
                "Recovery attempt skipped because payment lock " +
                "is held. HospitalId={HospitalId}",
                hospitalId);

            return null;
        }

        var attempt = await StartAttemptAsync(
            hospitalId,
            paymentSource,
            gatewayPaymentId,
            orderId,
            qrId,
            cancellationToken);

        await paymentLock.CommitAsync(cancellationToken);

        return attempt;
    }

    private static void ValidateRequest(
        int hospitalId,
        string paymentSource,
        string gatewayPaymentId,
        long? orderId,
        long? qrId)
    {
        if (hospitalId <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(hospitalId));

        if (string.IsNullOrWhiteSpace(gatewayPaymentId) ||
            gatewayPaymentId.Trim().Length > 150)
        {
            throw new ArgumentException(
                "A valid gateway payment ID is required.",
                nameof(gatewayPaymentId));
        }

        if (paymentSource is not ("Order" or "Qr"))
        {
            throw new ArgumentException(
                "Payment source must be Order or Qr.",
                nameof(paymentSource));
        }

        if (paymentSource == "Order" &&
            (!orderId.HasValue ||
             orderId.Value <= 0 ||
             qrId.HasValue))
        {
            throw new ArgumentException(
                "Order recovery requires exactly one valid OrderId.");
        }

        if (paymentSource == "Qr" &&
            (!qrId.HasValue ||
             qrId.Value <= 0 ||
             orderId.HasValue))
        {
            throw new ArgumentException(
                "QR recovery requires exactly one valid QrId.");
        }
    }

    /// <summary>
    /// Creates a new attempt after the payment lock is acquired.
    /// </summary>
    private async Task<HospitalPaymentRecoveryAttempt>
        StartAttemptAsync(
            int hospitalId,
            string paymentSource,
            string gatewayPaymentId,
            long? orderId,
            long? qrId,
            CancellationToken cancellationToken)
    {
        // Never start recovery if this gateway payment
        // already has a captured transaction.
        var captured = await _db.HospitalPaymentTransactions
            .AsNoTracking()
            .AnyAsync(x =>
                x.GatewayPaymentId == gatewayPaymentId &&
                x.Status == "Captured",
                cancellationToken);

        if (captured)
        {
            throw new InvalidOperationException(
                "Gateway payment already has a captured " +
                "transaction. No recovery attempt is required.");
        }

        // Confirm that the source is from the requested hospital.
        // An existing different gateway ID is a conflict.
        if (paymentSource == "Order")
        {
            var order = await _db.HospitalPaymentOrders
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.Id == orderId!.Value &&
                    x.HospitalId == hospitalId,
                    cancellationToken);

            if (order == null ||
                string.IsNullOrWhiteSpace(order.GatewayOrderId) ||
                order.Mode is not ("Test" or "Live"))
            {
                throw new InvalidOperationException(
                    "Hospital payment order is missing " +
                    "or has invalid gateway identity.");
            }

            if (!string.IsNullOrWhiteSpace(order.GatewayPaymentId) &&
                order.GatewayPaymentId != gatewayPaymentId)
            {
                throw new InvalidOperationException(
                    "Gateway payment ID conflicts with " +
                    "the existing order payment ID.");
            }
        }
        else
        {
            var qr = await _db.HospitalPaymentQrs
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.Id == qrId!.Value &&
                    x.HospitalId == hospitalId,
                    cancellationToken);

            if (qr == null ||
                string.IsNullOrWhiteSpace(qr.GatewayQrId) ||
                qr.Mode is not ("Test" or "Live"))
            {
                throw new InvalidOperationException(
                    "Hospital QR payment is missing " +
                    "or has invalid gateway identity.");
            }

            if (!string.IsNullOrWhiteSpace(qr.GatewayPaymentId) &&
                qr.GatewayPaymentId != gatewayPaymentId)
            {
                throw new InvalidOperationException(
                    "Gateway payment ID conflicts with " +
                    "the existing QR payment ID.");
            }
        }

        var history = await _db.HospitalPaymentRecoveryAttempts
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.GatewayPaymentId == gatewayPaymentId)
            .OrderByDescending(x => x.AttemptNumber)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (history != null)
        {
            if (history.PaymentSource != paymentSource ||
                history.HospitalPaymentOrderId != orderId ||
                history.HospitalPaymentQrId != qrId)
            {
                throw new InvalidOperationException(
                    "Recovery source conflicts with " +
                    "previous attempt history.");
            }

            if (history.Status is
                "Processing" or "Succeeded" or "ManualReview")
            {
                throw new InvalidOperationException(
                    "Payment is processing, recovered, " +
                    "or awaiting manual review.");
            }

            if (history.Status != "Failed")
            {
                throw new InvalidOperationException(
                    "Previous recovery status is not eligible for retry.");
            }

            if (history.AttemptNumber >= _maxAttempts)
            {
                throw new InvalidOperationException(
                    "Maximum recovery attempts reached. " +
                    "Manual review is required.");
            }

            if (history.NextRetryAtUtc == null)
            {
                throw new InvalidOperationException(
                    "No automatic retry has been scheduled.");
            }

            if (history.NextRetryAtUtc > DateTime.UtcNow)
            {
                throw new InvalidOperationException(
                    "Scheduled retry time has not been reached.");
            }
        }

        var attempt = new HospitalPaymentRecoveryAttempt
        {
            HospitalId = hospitalId,
            PaymentSource = paymentSource,
            GatewayPaymentId = gatewayPaymentId,
            HospitalPaymentOrderId = orderId,
            HospitalPaymentQrId = qrId,

            AttemptNumber =
                (history?.AttemptNumber ?? 0) + 1,

            Status = "Processing",
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = null,
            NextRetryAtUtc = null,
            ErrorMessage = null
        };

        _db.HospitalPaymentRecoveryAttempts.Add(attempt);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Recovery attempt {AttemptId} started. " +
            "HospitalId={HospitalId}, AttemptNumber={AttemptNumber}",
            attempt.Id,
            hospitalId,
            attempt.AttemptNumber);

        return attempt;
    }

    /// <summary>
    /// Completes an existing recovery attempt.
    ///
    /// Succeeded: payment was safely reconciled.
    /// Failed: retryable failure.
    /// ManualReview: unsafe or ambiguous outcome.
    /// </summary>
    public async Task CompleteAttemptAsync(
        long attemptId,
        string status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (status is not
            ("Succeeded" or "Failed" or "ManualReview"))
        {
            throw new ArgumentException(
                "Invalid recovery completion status.",
                nameof(status));
        }

        var attempt = await _db.HospitalPaymentRecoveryAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == attemptId,
                cancellationToken);

        if (attempt == null)
        {
            throw new InvalidOperationException(
                "Recovery attempt not found.");
        }

        if (attempt.Status != "Processing")
        {
            throw new InvalidOperationException(
                "Only processing attempts can be completed.");
        }

        var now = DateTime.UtcNow;

        string? safeError = string.IsNullOrWhiteSpace(errorMessage)
            ? null
            : errorMessage.Length <= 2000
                ? errorMessage
                : errorMessage[..2000];

        DateTime? nextRetry = null;

        if (status == "Failed" &&
            attempt.AttemptNumber < _maxAttempts)
        {
            var multiplier = 1L << Math.Min(
                attempt.AttemptNumber - 1,
                20);

            var delay = Math.Min(
                1440L,
                _initialRetryDelayMinutes * multiplier);

            nextRetry = now.AddMinutes(delay);
        }

        // Exhausted retry attempts must require manual review.
        if (status == "Failed" &&
            attempt.AttemptNumber >= _maxAttempts)
        {
            status = "ManualReview";
            nextRetry = null;

            safeError ??=
                "Maximum recovery attempts reached.";
        }

        var updated = await _db.HospitalPaymentRecoveryAttempts
            .Where(x =>
                x.Id == attemptId &&
                x.Status == "Processing")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, status)
                    .SetProperty(x => x.ErrorMessage, safeError)
                    .SetProperty(
                        x => x.CompletedAtUtc,
                        (DateTime?)now)
                    .SetProperty(
                        x => x.NextRetryAtUtc,
                        nextRetry),
                cancellationToken);

        if (updated != 1)
        {
            throw new InvalidOperationException(
                "Recovery attempt was changed concurrently.");
        }

        _logger.LogInformation(
            "Recovery attempt {AttemptId} completed. " +
            "Status={Status}, NextRetryAtUtc={NextRetry}",
            attemptId,
            status,
            nextRetry);
    }

    /// <summary>
    /// Marks stale Processing attempts as ManualReview.
    /// No payment allocation or Razorpay calls are performed.
    /// </summary>
    public async Task<int> MarkStaleAttemptsForReviewAsync(
        int minimumAgeMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        if (minimumAgeMinutes < 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumAgeMinutes),
                "Minimum age must be at least five minutes.");
        }

        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-minimumAgeMinutes);

        var updated = await _db.HospitalPaymentRecoveryAttempts
            .Where(x =>
                x.Status == "Processing" &&
                x.StartedAtUtc <= cutoff)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.Status,
                        "ManualReview")
                    .SetProperty(
                        x => x.ErrorMessage,
                        "Recovery attempt timed out. " +
                        "Verify Razorpay status and invoice " +
                        "allocation before taking further action.")
                    .SetProperty(
                        x => x.CompletedAtUtc,
                        (DateTime?)now)
                    .SetProperty(
                        x => x.NextRetryAtUtc,
                        (DateTime?)null),
                cancellationToken);

        if (updated > 0)
        {
            _logger.LogWarning(
                "{Count} stale recovery attempts " +
                "marked for manual review.",
                updated);
        }

        return updated;
    }
}
