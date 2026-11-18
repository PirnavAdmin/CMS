using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Services;

/// <summary>
/// Optional scheduler for settlement reconciliation. Does not directly modify
/// billing, captured payments, or refunds. The reconciliation service MUST
/// acquire the hospital/settlement SQL application lock internally for both
/// manual and scheduled entry points.
/// </summary>
public sealed class HospitalSettlementReconciliationBackgroundService : BackgroundService
{
    private const string Prefix = "Payments:HospitalSettlementReconciliation";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HospitalSettlementReconciliationBackgroundService> _logger;

    public HospitalSettlementReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<HospitalSettlementReconciliationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private bool IsEnabled() =>
        _configuration.GetValue<bool>($"{Prefix}:Enabled") &&
        _configuration.GetValue<bool>($"{Prefix}:GatewayEvidenceVerified");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
        {
            _logger.LogInformation("Hospital settlement reconciliation worker disabled by safety configuration.");
            return;
        }

        var intervalMinutes = Math.Clamp(_configuration.GetValue<int?>($"{Prefix}:IntervalMinutes") ?? 60, 15, 1440);
        var batchSize = Math.Clamp(_configuration.GetValue<int?>($"{Prefix}:BatchSize") ?? 10, 1, 100);
        var minimumAgeMinutes = Math.Clamp(_configuration.GetValue<int?>($"{Prefix}:MinimumAgeMinutes") ?? 60, 15, 10080);
        var maxAttempts = Math.Clamp(_configuration.GetValue<int?>($"{Prefix}:MaxAttempts") ?? 5, 1, 20);
        var retryDelayMinutes = Math.Clamp(_configuration.GetValue<int?>($"{Prefix}:RetryDelayMinutes") ?? 60, 15, 10080);

        _logger.LogWarning(
            "Hospital settlement reconciliation worker enabled: interval={IntervalMinutes}m, batch={BatchSize}, maxAttempts={MaxAttempts}, retryDelay={RetryDelayMinutes}m.",
            intervalMinutes, batchSize, maxAttempts, retryDelayMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!IsEnabled())
            {
                _logger.LogWarning("Hospital settlement reconciliation worker stopped: safety configuration disabled.");
                return;
            }

            try
            {
                await RunBatchAsync(batchSize, minimumAgeMinutes, maxAttempts, retryDelayMinutes, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hospital settlement reconciliation batch failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunBatchAsync(
        int batchSize,
        int minimumAgeMinutes,
        int maxAttempts,
        int retryDelayMinutes,
        CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var ageCutoff = now.AddMinutes(-minimumAgeMinutes);
        var retryCutoff = now.AddMinutes(-retryDelayMinutes);

        (long Id, int HospitalId)[] candidates;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.HospitalGatewaySettlements.AsNoTracking()
                .Where(x => x.HospitalId > 0 &&
                            (x.Mode == "Test" || x.Mode == "Live") &&
                            x.Status == "processed" &&
                            x.SettledAt <= ageCutoff &&
                            x.ReconciliationStatus == "Pending" &&
                            !x.RequiresManualReview &&
                            x.ReconciliationAttemptCount < maxAttempts &&
                            (x.LastReconciliationAttemptAtUtc == null ||
                             x.LastReconciliationAttemptAtUtc <= retryCutoff))
                .OrderBy(x => x.SettledAt)
                .ThenBy(x => x.Id)
                .Take(batchSize)
                .Select(x => new { x.Id, x.HospitalId })
                .ToListAsync(token);

            candidates = rows.Select(x => (x.Id, x.HospitalId)).ToArray();
        }

        foreach (var candidate in candidates)
        {
            token.ThrowIfCancellationRequested();
            if (!IsEnabled()) return;

            // Capture the state seen before invoking reconciliation. A competing
            // worker may complete or record a failure before this worker does.
            int? observedAttempts = null;
            DateTime? observedLastAttempt = null;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTime.UtcNow.AddMinutes(-retryDelayMinutes);

                // Recheck eligibility: another worker or an administrator may have
                // reconciled this row since the candidate list was read.
                var eligible = await db.HospitalGatewaySettlements.AsNoTracking()
                    .Where(x => x.Id == candidate.Id &&
                                x.HospitalId == candidate.HospitalId &&
                                (x.Mode == "Test" || x.Mode == "Live") &&
                                x.Status == "processed" &&
                                x.ReconciliationStatus == "Pending" &&
                                !x.RequiresManualReview &&
                                x.ReconciliationAttemptCount < maxAttempts &&
                                (x.LastReconciliationAttemptAtUtc == null ||
                                 x.LastReconciliationAttemptAtUtc <= cutoff))
                    .Select(x => new
                    {
                        x.ReconciliationAttemptCount,
                        x.LastReconciliationAttemptAtUtc
                    })
                    .SingleOrDefaultAsync(token);
                if (eligible == null) continue;
                observedAttempts = eligible.ReconciliationAttemptCount;
                observedLastAttempt = eligible.LastReconciliationAttemptAtUtc;
                if (!IsEnabled()) return;

                // IMPORTANT: ReconcileAsync must acquire the SQL app lock.
                // Do NOT acquire the same session-owned lock here as well:
                // a second connection would wait on the worker's own lock.
                var service = scope.ServiceProvider.GetRequiredService<HospitalSettlementReconciliationService>();
                var result = await service.ReconcileAsync(candidate.HospitalId, candidate.Id, token);
                _logger.LogInformation(
                    "Settlement reconciliation completed. HospitalId={HospitalId}, SettlementId={SettlementId}, Status={Status}.",
                    candidate.HospitalId, candidate.Id, result.Status);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Settlement reconciliation failed. HospitalId={HospitalId}, SettlementId={SettlementId}.",
                    candidate.HospitalId, candidate.Id);

                // A failed lock acquisition is contention, not a gateway retry.
                // Avoid consuming the settlement's attempt budget for it.
                if (ex is InvalidOperationException &&
                    ex.Message.StartsWith("Unable to acquire settlement reconciliation lock", StringComparison.Ordinal))
                {
                    _logger.LogWarning("Settlement lock unavailable; skipping retry count for {SettlementId}.", candidate.Id);
                    continue;
                }

                if (observedAttempts == null) continue;
                try
                {
                    await RecordFailureAsync(candidate.HospitalId, candidate.Id, maxAttempts,
                        observedAttempts.Value, observedLastAttempt, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception recordError)
                {
                    _logger.LogError(recordError,
                        "Failed to save reconciliation retry state. HospitalId={HospitalId}, SettlementId={SettlementId}.",
                        candidate.HospitalId, candidate.Id);
                }
            }
        }
    }

    private async Task RecordFailureAsync(
        int hospitalId,
        long settlementId,
        int maxAttempts,
        int observedAttempts,
        DateTime? observedLastAttempt,
        CancellationToken token)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var lockService = scope.ServiceProvider.GetRequiredService<HospitalSettlementReconciliationLockService>();

        // This is a separate lock acquisition AFTER ReconcileAsync has exited
        // and released its own lock. Never nest the two acquisitions.
        await lockService.ExecuteAsync(hospitalId, settlementId, async ct =>
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            // Compare the exact pre-attempt version of the retry state.
            // If another worker or the reconciliation service changed it,
            // do not charge this failed invocation as an additional attempt.
            var updated = await db.HospitalGatewaySettlements
                .Where(x => x.Id == settlementId &&
                            x.HospitalId == hospitalId &&
                            x.ReconciliationStatus == "Pending" &&
                            !x.RequiresManualReview &&
                            x.ReconciliationAttemptCount == observedAttempts &&
                            x.ReconciliationAttemptCount < maxAttempts &&
                            x.LastReconciliationAttemptAtUtc == observedLastAttempt)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ReconciliationAttemptCount, x => x.ReconciliationAttemptCount + 1)
                    .SetProperty(x => x.LastReconciliationAttemptAtUtc, (DateTime?)now)
                    .SetProperty(x => x.UpdatedAtUtc, (DateTime?)now)
                    .SetProperty(x => x.RequiresManualReview, x => x.ReconciliationAttemptCount + 1 >= maxAttempts)
                    .SetProperty(x => x.ReconciliationStatus,
                        x => x.ReconciliationAttemptCount + 1 >= maxAttempts ? "Review" : "Pending")
                    .SetProperty(x => x.ReconciliationNotes,
                        x => x.ReconciliationAttemptCount + 1 >= maxAttempts
                            ? "Automatic reconciliation failed repeatedly. Manual investigation required."
                            : "Automatic reconciliation attempt failed; retry scheduled."), ct);

            if (updated == 0)
                _logger.LogInformation(
                    "Retry state unchanged: another operation modified settlement {SettlementId}.", settlementId);
            else
                _logger.LogWarning(
                    "Recorded settlement failure under SQL lock. HospitalId={HospitalId} SettlementId={SettlementId}.",
                    hospitalId, settlementId);
            return updated;
        }, token);
    }
}
