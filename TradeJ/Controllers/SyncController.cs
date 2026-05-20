using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Services;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController(
    MT5AutoSyncService mt5Sync,
    CTraderAutoSyncService ctraderSync,
    AppDbContext db,
    ILogger<SyncController> logger) : ControllerBase
{
    private const int ManualLookbackDays = 30;

    /// <summary>Manually trigger a full sync of all MT5 and cTrader accounts immediately.</summary>
    [HttpPost("all")]
    public async Task<IActionResult> SyncAll(CancellationToken ct)
    {
        logger.LogInformation("Manual sync triggered from dashboard.");
        await mt5Sync.TriggerSyncAsync(ct);
        await ctraderSync.TriggerSyncAsync(ct);
        return Ok();
    }

    /// <summary>Sync only the specified account IDs, returning per-account results.</summary>
    [HttpPost("selected")]
    public async Task<ActionResult<SyncSelectedResultDto>> SyncSelected(
        [FromBody] int[] accountIds,
        [FromServices] MT5BridgeImportService bridgeService,
        [FromServices] CTraderApiService ctraderService,
        CancellationToken ct)
    {
        if (accountIds == null || accountIds.Length == 0)
            return BadRequest("No account IDs provided.");

        logger.LogInformation("Manual sync for {Count} selected account(s).", accountIds.Length);

        var accounts = await db.Accounts
            .Where(a => a.IsActive && accountIds.Contains(a.Id))
            .ToListAsync(ct);

        var dateTo   = DateTime.UtcNow;
        var dateFrom = dateTo.AddDays(-ManualLookbackDays);

        int totalImported = 0, totalSkipped = 0, totalErrors = 0;
        var messages = new List<string>();

        foreach (var account in accounts)
        {
            if (ct.IsCancellationRequested) break;

            // MT5 bridge sync
            if (!string.IsNullOrEmpty(account.MT5Server) && !string.IsNullOrEmpty(account.MT5InvestorPassword))
            {
                try
                {
                    var r = await bridgeService.ImportAsync(
                        account.Id, account.AccountNumber,
                        account.MT5InvestorPassword, account.MT5Server,
                        dateFrom, dateTo);

                    totalImported += r.Imported;
                    totalSkipped  += r.Skipped;
                    totalErrors   += r.Errors;
                    messages.AddRange(r.ErrorMessages.Select(m => $"[{account.Name}] {m}"));

                    logger.LogInformation("Sync [{Name}] MT5: imported {I}, skipped {S}, errors {E}.",
                        account.Name, r.Imported, r.Skipped, r.Errors);
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    var msg = $"[{account.Name}] MT5 sync failed: {ex.Message}";
                    messages.Add(msg);
                    logger.LogError(ex, "Sync [{Name}] MT5 unexpected error.", account.Name);
                }
            }

            // cTrader sync
            if (account.CTraderCtidAccountId.HasValue && !string.IsNullOrEmpty(account.CTraderRefreshToken))
            {
                try
                {
                    var accessToken = await ctraderService.RefreshAccessTokenAsync(account.CTraderRefreshToken);
                    var r = await ctraderService.ImportAsync(new CTraderImportRequest(
                        AccessToken:         accessToken,
                        CtidTraderAccountId: account.CTraderCtidAccountId.Value,
                        IsLive:              account.CTraderIsLive,
                        TradeJAccountId:     account.Id,
                        DateFrom:            dateFrom,
                        DateTo:              dateTo));

                    totalImported += r.Imported;
                    totalSkipped  += r.Skipped;
                    totalErrors   += r.Errors;
                    messages.AddRange(r.ErrorMessages.Select(m => $"[{account.Name}] {m}"));

                    logger.LogInformation("Sync [{Name}] cTrader: imported {I}, skipped {S}, errors {E}.",
                        account.Name, r.Imported, r.Skipped, r.Errors);
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    var msg = $"[{account.Name}] cTrader sync failed: {ex.Message}";
                    messages.Add(msg);
                    logger.LogError(ex, "Sync [{Name}] cTrader unexpected error.", account.Name);
                }
            }
        }

        return Ok(new SyncSelectedResultDto(totalImported, totalSkipped, totalErrors, messages));
    }
}

public record SyncSelectedResultDto(int Imported, int Skipped, int Errors, List<string> ErrorMessages);
