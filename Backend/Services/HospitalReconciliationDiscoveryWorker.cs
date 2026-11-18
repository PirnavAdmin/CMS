using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Services;

/// <summary>
/// Discovers unresolved hospital Razorpay payments and, only when explicitly enabled,
/// invokes the verified payment recovery executor in small sequential batches.
/// No new payments or refunds are created by this worker.
/// </summary>
public sealed class HospitalReconciliationDiscoveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<HospitalReconciliationDiscoveryWorker> _logger;

    private sealed record Candidate(int HospitalId, string Source, long SourceId,
        string PaymentId, DateTime CreatedAtUtc);

    public HospitalReconciliationDiscoveryWorker(
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILogger<HospitalReconciliationDiscoveryWorker> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue<bool>("HospitalReconciliationWorker:Enabled"))
        {
            _logger.LogInformation("Hospital reconciliation worker is disabled.");
            return;
        }

        var intervalMinutes = Math.Clamp(
            _config.GetValue("HospitalReconciliationWorker:IntervalMinutes", 30), 5, 1440);
        var automatic = _config.GetValue<bool>(
            "HospitalPaymentRecovery:AutomaticAllocationEnabled");

        _logger.LogWarning(
            "Hospital reconciliation worker started. Automatic allocation enabled={Automatic}",
            automatic);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));
        try
        {
            do
            {
                try
                {
                    await RunIterationAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Hospital reconciliation iteration failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task RunIterationAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // A SQL Server session lease prevents overlapping worker iterations
        // across application instances sharing the same database.
        await db.Database.OpenConnectionAsync(ct);
        var connection = db.Database.GetDbConnection();
        var acquired = false;
        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
DECLARE @result INT;
EXEC @result = sp_getapplock
    @Resource = N'Adminclinic:HospitalReconciliationDiscovery:v1',
    @LockMode = N'Exclusive',
    @LockOwner = N'Session',
    @LockTimeout = 0;
SELECT @result;";
                acquired = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) >= 0;
            }

            if (!acquired)
            {
                _logger.LogInformation("Another reconciliation worker holds the lease.");
                return;
            }

            var minimumAge = Math.Clamp(
                _config.GetValue("HospitalReconciliationWorker:MinimumAgeMinutes", 15),
                1, 1440);
            var cutoff = DateTime.UtcNow.AddMinutes(-minimumAge);
            var limit = Math.Clamp(
                _config.GetValue("HospitalReconciliationWorker:BatchSize", 10), 1, 25);

            // Read-only discovery; no Razorpay calls in these queries.
            var orders = await db.HospitalPaymentOrders.AsNoTracking()
                .Where(x => x.CreatedAt <= cutoff &&
                    x.GatewayPaymentId != null && x.GatewayPaymentId != "" &&
                    x.GatewayOrderId != null && x.GatewayOrderId != "" &&
                    !db.HospitalPaymentTransactions.Any(t =>
                        t.GatewayPaymentId == x.GatewayPaymentId && t.Status == "Captured"))
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .Take(limit)
                .Select(x => new Candidate(x.HospitalId, "Order", x.Id,
                    x.GatewayPaymentId!, x.CreatedAt))
                .ToListAsync(ct);

            var qrs = await db.HospitalPaymentQrs.AsNoTracking()
                .Where(x => x.CreatedAtUtc <= cutoff &&
                    x.GatewayPaymentId != null && x.GatewayPaymentId != "" &&
                    x.GatewayQrId != null && x.GatewayQrId != "" &&
                    !db.HospitalPaymentTransactions.Any(t =>
                        t.GatewayPaymentId == x.GatewayPaymentId && t.Status == "Captured"))
                .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
                .Take(limit)
                .Select(x => new Candidate(x.HospitalId, "Qr", x.Id,
                    x.GatewayPaymentId!, x.CreatedAtUtc))
                .ToListAsync(ct);

            var selected = orders.Concat(qrs)
                .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.SourceId)
                .Take(limit).ToList();

            // The same gateway payment ID in multiple sources is ambiguous.
            // Never guess which invoice to credit.
            var duplicates = selected.GroupBy(x => x.PaymentId, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key).ToHashSet(StringComparer.Ordinal);

            var automatic = _config.GetValue<bool>(
                "HospitalPaymentRecovery:AutomaticAllocationEnabled");
            var now = DateTime.UtcNow;
            foreach (var group in selected.GroupBy(x => x.HospitalId))
            {
                db.AuditLogs.Add(new AuditLog
                {
                    UserId = 0,
                    UserName = "System",
                    Role = "System",
                    ClinicId = group.Key,
                    Action = "HospitalReconciliationDiscovery",
                    Timestamp = now,
                    LoginTime = now,
                    SystemAction = $"Candidates={group.Count()}; " +
                        $"orderCandidates={group.Count(x => x.Source == "Order")}; " +
                        $"qrCandidates={group.Count(x => x.Source == "Qr")}; " +
                        $"minimumAgeMinutes={minimumAge}; " +
                        $"automaticAllocationEnabled={automatic}; " +
                        $"ambiguousPaymentIds={group.Count(x => duplicates.Contains(x.PaymentId))}"
                });
            }
            if (selected.Count > 0)
                await db.SaveChangesAsync(ct);

            if (automatic)
            {
                // A separate DI scope per payment avoids sharing one DbContext
                // across gateway calls or recovering from an EF failure.
                foreach (var candidate in selected)
                {
                    ct.ThrowIfCancellationRequested();
                    if (duplicates.Contains(candidate.PaymentId))
                    {
                        _logger.LogError(
                            "Ambiguous gateway payment ID in discovered sources. Hospital={HospitalId}",
                            candidate.HospitalId);
                        continue;
                    }

                    try
                    {
                        await using var itemScope = _scopes.CreateAsyncScope();
                        var executor = itemScope.ServiceProvider
                            .GetRequiredService<HospitalPaymentRecoveryExecutor>();
                        var result = await executor.ExecuteAsync(
                            candidate.HospitalId,
                            candidate.Source,
                            candidate.PaymentId,
                            orderId: candidate.Source == "Order" ? candidate.SourceId : null,
                            qrId: candidate.Source == "Qr" ? candidate.SourceId : null,
                            cancellationToken: ct);

                        _logger.LogInformation(
                            "Recovery candidate processed. Hospital={HospitalId}, Source={Source}, Status={Status}",
                            candidate.HospitalId, candidate.Source, result.Status);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Recovery candidate failed. Hospital={HospitalId}, Source={Source}",
                            candidate.HospitalId, candidate.Source);
                    }
                }
            }
            else
            {
                _logger.LogInformation(
                    "Read-only reconciliation discovery found {Count} candidates.",
                    selected.Count);
            }
        }
        finally
        {
            if (acquired)
            {
                try
                {
                    await using var release = connection.CreateCommand();
                    release.CommandText = @"
EXEC sp_releaseapplock
    @Resource = N'Adminclinic:HospitalReconciliationDiscovery:v1',
    @LockOwner = N'Session';";
                    await release.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to release reconciliation session lease.");
                }
            }
            await db.Database.CloseConnectionAsync();
        }

        // Separate from discovery lease; stale attempts always require review.
        await using var staleScope = _scopes.CreateAsyncScope();
        var recovery = staleScope.ServiceProvider
            .GetRequiredService<HospitalPaymentRecoveryService>();
        var staleMinutes = Math.Clamp(
            _config.GetValue("HospitalReconciliationWorker:StaleRecoveryMinutes", 30),
            5, 1440);
        var stale = await recovery.MarkStaleAttemptsForReviewAsync(staleMinutes, ct);
        if (stale > 0)
            _logger.LogWarning("Marked {Count} stale recovery attempts for manual review.", stale);
    }
}
