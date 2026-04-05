using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;

namespace TradeJ.Services;

/// <summary>
/// Periodically resyncs every active cTrader account that has a stored refresh token.
/// Uses the same interval/look-back config as MT5AutoSyncService (AutoSync section).
/// </summary>
public sealed class CTraderAutoSyncService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    AppSettingsService appSettings,
    ILogger<CTraderAutoSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = config.GetValue("AutoSync:IntervalMinutes", 5);
        var lookbackDays    = config.GetValue("AutoSync:LookbackDays", 7);
        var interval        = TimeSpan.FromMinutes(intervalMinutes);

        logger.LogInformation(
            "cTrader auto-sync started — every {Interval} min, look-back {Days} days.",
            intervalMinutes, lookbackDays);

        // Stagger start so it doesn't hit the API at the same instant as MT5 sync.
        await Task.Delay(interval + TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (appSettings.CTraderSyncEnabled)
                await SyncAllAccountsAsync(lookbackDays, stoppingToken);
            else
                logger.LogDebug("cTrader auto-sync is disabled, skipping tick.");

            await Task.Delay(interval, stoppingToken);
        }
    }

    /// <summary>Manually trigger one sync cycle (e.g. from the dashboard button).</summary>
    public Task TriggerSyncAsync(CancellationToken ct = default)
    {
        var lookbackDays = config.GetValue("AutoSync:LookbackDays", 7);
        return SyncAllAccountsAsync(lookbackDays, ct);
    }

    private async Task SyncAllAccountsAsync(int lookbackDays, CancellationToken ct)
    {
        await using var scope   = scopeFactory.CreateAsyncScope();
        var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctrader = scope.ServiceProvider.GetRequiredService<CTraderApiService>();

        var accounts = await db.Accounts
            .Where(a => a.IsActive
                     && a.CTraderCtidAccountId != null
                     && a.CTraderRefreshToken != null && a.CTraderRefreshToken != "")
            .ToListAsync(ct);

        if (accounts.Count == 0)
        {
            logger.LogDebug("cTrader auto-sync: no linked accounts, skipping.");
            return;
        }

        var dateTo   = DateTime.UtcNow;
        var dateFrom = dateTo.AddDays(-lookbackDays);

        logger.LogInformation(
            "cTrader auto-sync: syncing {Count} account(s) from {From:yyyy-MM-dd} to {To:yyyy-MM-dd}.",
            accounts.Count, dateFrom, dateTo);

        foreach (var account in accounts)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                // Refresh the token — stores new refresh token implicitly via cTrader API response.
                // (Spotware may rotate the refresh token on each use.)
                var accessToken = await ctrader.RefreshAccessTokenAsync(account.CTraderRefreshToken!);

                var result = await ctrader.ImportAsync(new CTraderImportRequest(
                    AccessToken:         accessToken,
                    CtidTraderAccountId: account.CTraderCtidAccountId!.Value,
                    IsLive:              account.CTraderIsLive,
                    TradeJAccountId:     account.Id,
                    DateFrom:            dateFrom,
                    DateTo:              dateTo));

                logger.LogInformation(
                    "cTrader auto-sync [{Name}]: imported {Imported}, skipped {Skipped}, errors {Errors}.",
                    account.Name, result.Imported, result.Skipped, result.Errors);

                if (result.Errors > 0)
                    logger.LogWarning(
                        "cTrader auto-sync [{Name}] errors: {Msgs}",
                        account.Name, string.Join("; ", result.ErrorMessages));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "cTrader auto-sync [{Name}]: unexpected error.", account.Name);
            }
        }
    }
}
