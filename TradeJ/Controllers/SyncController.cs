using Microsoft.AspNetCore.Mvc;
using TradeJ.Services;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController(
    MT5AutoSyncService mt5Sync,
    CTraderAutoSyncService ctraderSync,
    ILogger<SyncController> logger) : ControllerBase
{
    /// <summary>Manually trigger a full sync of all MT5 and cTrader accounts immediately.</summary>
    [HttpPost("all")]
    public async Task<IActionResult> SyncAll(CancellationToken ct)
    {
        logger.LogInformation("Manual sync triggered from dashboard.");
        await mt5Sync.TriggerSyncAsync(ct);
        await ctraderSync.TriggerSyncAsync(ct);
        return Ok();
    }

    /// <summary>Sync only the specified account IDs.</summary>
    [HttpPost("selected")]
    public async Task<IActionResult> SyncSelected([FromBody] int[] accountIds, CancellationToken ct)
    {
        if (accountIds == null || accountIds.Length == 0)
            return BadRequest("No account IDs provided.");

        logger.LogInformation("Manual sync triggered for {Count} selected account(s).", accountIds.Length);
        await mt5Sync.TriggerSyncForAccountsAsync(accountIds, ct);
        await ctraderSync.TriggerSyncForAccountsAsync(accountIds, ct);
        return Ok();
    }
}
