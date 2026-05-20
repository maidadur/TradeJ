using Microsoft.EntityFrameworkCore;
using TradeJ.Data;

namespace TradeJ.Services;

/// <summary>
/// Runs in the background and resyncs every active MT5 account that has
/// the bridge credentials configured (MT5Server + MT5InvestorPassword).
///
/// Config keys (appsettings.json → AutoSync section):
///   AutoSync:IntervalMinutes   – how often to run (default 5)
///   AutoSync:LookbackDays      – how far back to pull deals (default 7)
/// </summary>
public sealed class MT5AutoSyncService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    AppSettingsService appSettings,
    ILogger<MT5AutoSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = config.GetValue("AutoSync:IntervalMinutes", 5);
        var lookbackDays    = config.GetValue("AutoSync:LookbackDays", 7);
        var interval        = TimeSpan.FromMinutes(intervalMinutes);

        logger.LogInformation(
            "MT5 auto-sync started — every {Interval} min, look-back {Days} days.",
            intervalMinutes, lookbackDays);

        // Wait one full interval before the first run so the app finishes starting up.
        await Task.Delay(interval, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (appSettings.Mt5SyncEnabled)
                await SyncAllAccountsAsync(lookbackDays, null, stoppingToken);
            else
                logger.LogDebug("MT5 auto-sync is disabled, skipping tick.");

            await Task.Delay(interval, stoppingToken);
        }
    }

    /// <summary>Manually trigger one sync cycle (e.g. from the dashboard button).</summary>
    public Task TriggerSyncAsync(CancellationToken ct = default)
    {
        var lookbackDays = config.GetValue("AutoSync:LookbackDays", 7);
        return SyncAllAccountsAsync(lookbackDays, null, ct);
    }

    /// <summary>Manually trigger a sync cycle for specific account IDs only.</summary>
    public Task TriggerSyncForAccountsAsync(int[] accountIds, int? lookbackDaysOverride = null, CancellationToken ct = default)
    {
        var lookbackDays = lookbackDaysOverride ?? config.GetValue("AutoSync:LookbackDays", 7);
        return SyncAllAccountsAsync(lookbackDays, accountIds, ct);
    }

    private async Task SyncAllAccountsAsync(int lookbackDays, int[]? filterIds, CancellationToken ct)
    {
        // Each tick gets its own DI scope so EF Core and HttpClient are fresh.
        await using var scope = scopeFactory.CreateAsyncScope();
        var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bridge  = scope.ServiceProvider.GetRequiredService<MT5BridgeImportService>();

        var query = db.Accounts
            .Where(a => a.IsActive
                     && a.MT5Server != null && a.MT5Server != ""
                     && a.MT5InvestorPassword != null && a.MT5InvestorPassword != "");

        if (filterIds != null && filterIds.Length > 0)
            query = query.Where(a => filterIds.Contains(a.Id));

        var accounts = await query.ToListAsync(ct);

        if (accounts.Count == 0)
        {
            logger.LogDebug("Auto-sync: no MT5 bridge accounts configured, skipping.");
            return;
        }

        var dateTo   = DateTime.UtcNow;
        var dateFrom = dateTo.AddDays(-lookbackDays);

        logger.LogInformation(
            "Auto-sync: syncing {Count} account(s) from {From:yyyy-MM-dd} to {To:yyyy-MM-dd}.",
            accounts.Count, dateFrom, dateTo);

        foreach (var account in accounts)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var result = await bridge.ImportAsync(
                    account.Id,
                    account.AccountNumber,
                    account.MT5InvestorPassword!,
                    account.MT5Server!,
                    dateFrom,
                    dateTo);

                logger.LogInformation(
                    "Auto-sync [{Name}]: imported {Imported}, skipped {Skipped}, errors {Errors}.",
                    account.Name, result.Imported, result.Skipped, result.Errors);

                if (result.Errors > 0)
                    logger.LogWarning(
                        "Auto-sync [{Name}] errors: {Msgs}",
                        account.Name, string.Join("; ", result.ErrorMessages));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Auto-sync [{Name}]: unexpected error.", account.Name);
            }
        }
    }
}
