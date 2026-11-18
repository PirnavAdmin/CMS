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
/// Synchronizes provider settlement summaries into separate hospital and CMS ledgers.
/// This is NOT transaction-level settlement reconciliation.
/// Disabled by default using Payments:SettlementSync:Enabled.
/// </summary>
public sealed class RazorpaySettlementSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RazorpaySettlementSyncBackgroundService> _logger;

    public RazorpaySettlementSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RazorpaySettlementSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("Payments:SettlementSync:Enabled"))
        {
            _logger.LogInformation("Razorpay settlement background sync is disabled.");
            return;
        }

        var intervalHours = Math.Clamp(
            _configuration.GetValue<int?>("Payments:SettlementSync:IntervalHours") ?? 24, 1, 168);
        var lookbackDays = Math.Clamp(
            _configuration.GetValue<int?>("Payments:SettlementSync:LookbackDays") ?? 7, 1, 93);

        _logger.LogInformation(
            "Razorpay settlement sync enabled. IntervalHours={IntervalHours}, LookbackDays={LookbackDays}",
            intervalHours, lookbackDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllAsync(lookbackDays, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled Razorpay settlement sync failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SyncAllAsync(int lookbackDays, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var to = DateTime.UtcNow;
        var from = to.AddDays(-lookbackDays);

        var hospitals = await db.HospitalMerchantSettings
            .AsNoTracking()
            .Where(x => x.KycStatus == "Approved" &&
                        x.ProviderMerchantAccountId != null &&
                        x.ProviderMerchantAccountId != "")
            .Select(x => new { x.HospitalId, x.Mode })
            .ToListAsync(cancellationToken);

        // Do not sync an unexpected mode or process the same merchant twice.
        foreach (var merchant in hospitals
                     .Where(x => x.HospitalId > 0 && (x.Mode == "Test" || x.Mode == "Live"))
                     .Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var merchantScope = _scopeFactory.CreateAsyncScope();
            var merchantDb = merchantScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gateway = merchantScope.ServiceProvider.GetRequiredService<HospitalRazorpayGatewayService>();

            try
            {
                var settlements = await gateway.FetchSettlementsAsync(
                    merchant.HospitalId, merchant.Mode, from, to);

                if (settlements == null)
                {
                    _logger.LogWarning(
                        "Null settlement response for hospital {HospitalId}, mode {Mode}.",
                        merchant.HospitalId, merchant.Mode);
                    continue;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var synced = 0;

                foreach (var item in settlements)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (item == null || string.IsNullOrWhiteSpace(item.Id))
                    {
                        _logger.LogWarning(
                            "Skipping hospital settlement with missing identity. HospitalId={HospitalId}, Mode={Mode}",
                            merchant.HospitalId, merchant.Mode);
                        continue;
                    }

                    var settlementId = item.Id.Trim();
                    if (!seen.Add(settlementId))
                        continue;

                    if (item.Amount < 0 || item.Fees < 0 || item.Tax < 0)
                    {
                        _logger.LogWarning(
                            "Skipping settlement with negative financial values. HospitalId={HospitalId}, SettlementId={SettlementId}",
                            merchant.HospitalId, settlementId);
                        continue;
                    }

                    var row = await merchantDb.HospitalGatewaySettlements
                        .FirstOrDefaultAsync(x =>
                            x.HospitalId == merchant.HospitalId &&
                            x.Mode == merchant.Mode &&
                            x.SettlementId == settlementId,
                            cancellationToken);

                    if (row == null)
                    {
                        row = new HospitalGatewaySettlement
                        {
                            HospitalId = merchant.HospitalId,
                            Mode = merchant.Mode,
                            SettlementId = settlementId
                        };
                        merchantDb.HospitalGatewaySettlements.Add(row);
                    }

                    row.Status = item.Status;
                    row.Amount = item.Amount;
                    row.FeeAmount = item.Fees;
                    row.TaxAmount = item.Tax;
                    row.Utr = item.Utr;
                    row.SettledAt = item.CreatedAt;
                    row.SyncedAt = DateTime.UtcNow;
                    synced++;
                }

                await merchantDb.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Hospital settlement summaries synced. HospitalId={HospitalId}, Mode={Mode}, Count={Count}",
                    merchant.HospitalId, merchant.Mode, synced);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Settlement sync failed for hospital {HospitalId}, mode {Mode}.",
                    merchant.HospitalId, merchant.Mode);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var cmsMerchant = await db.CmsOwnerMerchantSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (cmsMerchant == null ||
            cmsMerchant.KycStatus != "Approved" ||
            string.IsNullOrWhiteSpace(cmsMerchant.ProviderMerchantAccountId) ||
            (cmsMerchant.Mode != "Test" && cmsMerchant.Mode != "Live"))
        {
            _logger.LogDebug("CMS owner settlement sync skipped: eligible merchant not configured.");
            return;
        }

        await using var cmsScope = _scopeFactory.CreateAsyncScope();
        var cmsDb = cmsScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cmsGateway = cmsScope.ServiceProvider.GetRequiredService<CmsSubscriptionRazorpayService>();

        try
        {
            var settlements = await cmsGateway.FetchSettlementsAsync(cmsMerchant.Mode, from, to);
            if (settlements == null)
            {
                _logger.LogWarning("Null CMS settlement response. Mode={Mode}", cmsMerchant.Mode);
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var synced = 0;

            foreach (var item in settlements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                {
                    _logger.LogWarning("Skipping CMS settlement with missing identity. Mode={Mode}", cmsMerchant.Mode);
                    continue;
                }

                var settlementId = item.Id.Trim();
                if (!seen.Add(settlementId))
                    continue;

                if (item.Amount < 0 || item.Fees < 0 || item.Tax < 0)
                {
                    _logger.LogWarning(
                        "Skipping CMS settlement with negative financial values. SettlementId={SettlementId}",
                        settlementId);
                    continue;
                }

                var row = await cmsDb.CmsGatewaySettlements
                    .FirstOrDefaultAsync(x =>
                        x.Mode == cmsMerchant.Mode &&
                        x.SettlementId == settlementId,
                        cancellationToken);

                if (row == null)
                {
                    row = new CmsGatewaySettlement
                    {
                        Mode = cmsMerchant.Mode,
                        SettlementId = settlementId
                    };
                    cmsDb.CmsGatewaySettlements.Add(row);
                }

                row.Status = item.Status;
                row.Amount = item.Amount;
                row.FeeAmount = item.Fees;
                row.TaxAmount = item.Tax;
                row.Utr = item.Utr;
                row.SettledAt = item.CreatedAt;
                row.SyncedAt = DateTime.UtcNow;
                synced++;
            }

            await cmsDb.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "CMS settlement summaries synced. Mode={Mode}, Count={Count}",
                cmsMerchant.Mode, synced);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Settlement sync failed for CMS owner, mode {Mode}.", cmsMerchant.Mode);
        }
    }
}
