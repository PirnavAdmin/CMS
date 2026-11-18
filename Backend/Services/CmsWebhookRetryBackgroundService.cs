using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Services;

// CMS payment.captured recovery worker. Disabled by default.
// A MySQL named lock prevents concurrent retry batches across instances.
public sealed class CmsWebhookRetryBackgroundService : BackgroundService
{
    private const string LockName = "CMS:Razorpay:WebhookRetry:payment.captured";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CmsWebhookRetryBackgroundService> _logger;

    public CmsWebhookRetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<CmsWebhookRetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("Payments:CmsWebhookRetry:Enabled"))
        {
            _logger.LogInformation("CMS webhook retry background service is disabled.");
            return;
        }

        var intervalSeconds = Math.Clamp(
            _configuration.GetValue<int?>("Payments:CmsWebhookRetry:IntervalSeconds") ?? 120, 30, 3600);
        var batchSize = Math.Clamp(
            _configuration.GetValue<int?>("Payments:CmsWebhookRetry:BatchSize") ?? 20, 1, 100);
        var maxAttempts = Math.Clamp(
            _configuration.GetValue<int?>("Payments:CmsWebhookRetry:MaxAttempts") ?? 5, 1, 20);

        try
        {
            await RunBatchSafelyAsync(batchSize, maxAttempts, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunBatchSafelyAsync(batchSize, maxAttempts, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task RunBatchSafelyAsync(int batchSize, int maxAttempts, CancellationToken token)
    {
        try
        {
            await RetryBatchAsync(batchSize, maxAttempts, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CMS webhook retry batch failed.");
        }
    }

    private async Task RetryBatchAsync(int batchSize, int maxAttempts, CancellationToken token)
    {
        // Session-scoped MySQL named lock, held until the entire batch finishes.
        // A separate connection is used so EF transactions do not release this lock.
        await using var lockScope = _scopeFactory.CreateAsyncScope();
        var lockDb = lockScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionString = lockDb.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("CMS webhook retry requires a MySQL connection string.");

        await using var lockConnection = new MySqlConnection(connectionString);
        await lockConnection.OpenAsync(token);

        await using (var command = lockConnection.CreateCommand())
        {
            command.CommandText = "SELECT GET_LOCK(@resource, 0)";
            command.Parameters.AddWithValue("@resource", LockName);
            var result = await command.ExecuteScalarAsync(token);
            if (Convert.ToInt32(result) != 1)
                return; // Another instance is already running the batch.
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            // Without LastAttemptAt, ReceivedAt can only provide a conservative
            // age-based schedule. This is not a true per-attempt backoff.
            var candidates = await db.CmsPaymentWebhookEvents.AsNoTracking()
                .Where(x => x.EventType == "payment.captured" &&
                            (x.ProcessingStatus == "Received" || x.ProcessingStatus == "Failed") &&
                            x.AttemptCount < maxAttempts)
                .OrderBy(x => x.ReceivedAt)
                .Select(x => new { x.Id, x.ReceivedAt, x.AttemptCount })
                .Take(batchSize * 5)
                .ToListAsync(token);

            var ids = candidates
                .Where(x => x.ReceivedAt <= now.Subtract(TimeSpan.FromMinutes(
                    Math.Min(60, 2 * (1 << Math.Min(x.AttemptCount, 5))))))
                .Take(batchSize)
                .Select(x => x.Id)
                .ToList();

            foreach (var id in ids)
            {
                token.ThrowIfCancellationRequested();
                await ProcessOneAsync(id, token);
            }
        }
        finally
        {
            try
            {
                await using var command = lockConnection.CreateCommand();
                command.CommandText = "SELECT RELEASE_LOCK(@resource)";
                command.Parameters.AddWithValue("@resource", LockName);
                await command.ExecuteScalarAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CMS webhook retry lock release failed; MySQL session disposal will release it.");
            }
        }
    }

    private async Task ProcessOneAsync(long id, CancellationToken token)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<CmsRazorpayReconciliationService>();
            var result = await service.ProcessAsync(id);
            if (result == "Processed" || result == "AlreadyProcessed")
                return;

            await RecordFailureAsync(id, result, token);
            _logger.LogWarning("CMS webhook retry returned {Result} for event {EventId}.", result, id);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CMS webhook retry failed for event {EventId}.", id);
            try
            {
                await RecordFailureAsync(id, ex.GetType().Name, token);
            }
            catch (Exception recordEx) when (!token.IsCancellationRequested)
            {
                _logger.LogError(recordEx, "Could not record CMS webhook retry failure for event {EventId}.", id);
            }
        }
    }

    private async Task RecordFailureAsync(long id, string reason, CancellationToken token)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // The reconciliation transaction may have committed before an exception.
        // Recheck committed state before marking a failure.
        var webhook = await db.CmsPaymentWebhookEvents.SingleOrDefaultAsync(x => x.Id == id, token);
        if (webhook == null || webhook.ProcessingStatus == "Processed")
            return;

        webhook.AttemptCount++;
        webhook.LastError = reason.Length > 1000 ? reason[..1000] : reason;
        webhook.ProcessingStatus = "Failed";
        await db.SaveChangesAsync(token);
    }
}
