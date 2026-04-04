using Microsoft.AspNetCore.Mvc;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Services;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> AllowedBrokers =
        new(StringComparer.OrdinalIgnoreCase) { "mt5", "ctrader", "bybit" };

    private const long MaxFileSize = 20 * 1024 * 1024; // 20 MB

    [HttpPost("{broker}")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ImportResultDto>> Import(
        string broker,
        [FromQuery] int accountId,
        IFormFile file)
    {
        if (!AllowedBrokers.Contains(broker))
            return BadRequest(new { message = $"Unknown broker '{broker}'. Use: mt5, ctrader, bybit." });

        var account = await db.Accounts.FindAsync(accountId);
        if (account is null)
            return BadRequest(new { message = $"Account {accountId} not found." });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "File exceeds the 20MB size limit." });

        IImportService service = broker.ToLowerInvariant() switch
        {
            "mt5"     => new MT5ImportService(db),
            "ctrader" => new CTraderImportService(db),
            "bybit"   => new ByBitImportService(db),
            _         => throw new InvalidOperationException()
        };

        await using var stream = file.OpenReadStream();
        var result = await service.ImportAsync(accountId, stream, file.FileName);
        return Ok(result);
    }

    [HttpPost("mt5-live")]
    public async Task<ActionResult<ImportResultDto>> ImportMt5Live(
        [FromQuery] int accountId,
        [FromBody] MT5LiveImportRequestDto request,
        [FromServices] MT5LiveImportService liveService)
    {
        var account = await db.Accounts.FindAsync(accountId);
        if (account is null)
            return BadRequest(new { message = $"Account {accountId} not found." });

        if (string.IsNullOrWhiteSpace(account.MetaApiAccountId) ||
            string.IsNullOrWhiteSpace(account.MetaApiToken))
            return BadRequest(new { message = "MetaApi credentials not configured for this account. Go to Accounts to set them up." });

        var result = await liveService.ImportAsync(
            accountId,
            account.MetaApiToken,
            account.MetaApiAccountId,
            account.MetaApiRegion,
            request.DateFrom,
            request.DateTo);

        return Ok(result);
    }

    /// <summary>
    /// Sync trades via a locally running Python bridge (scripts/mt5_bridge.py).
    /// Investor password is read from the stored account — never accepted from the client.
    /// </summary>
    [HttpPost("mt5-sync")]
    public async Task<ActionResult<ImportResultDto>> ImportMt5Sync(
        [FromQuery] int accountId,
        [FromBody] MT5SyncRequestDto request,
        [FromServices] MT5BridgeImportService bridgeService)
    {
        var account = await db.Accounts.FindAsync(accountId);
        if (account is null)
            return BadRequest(new { message = $"Account {accountId} not found." });

        if (string.IsNullOrWhiteSpace(account.MT5Server))
            return BadRequest(new { message = "MT5 Server not configured for this account. Go to Accounts → edit the account and fill in the MT5 Server field." });

        if (string.IsNullOrWhiteSpace(account.MT5InvestorPassword))
            return BadRequest(new { message = "Investor password not configured for this account. Go to Accounts → edit the account and set the MT5 Investor Password." });

        var result = await bridgeService.ImportAsync(
            accountId,
            account.AccountNumber,
            account.MT5InvestorPassword,
            account.MT5Server,
            request.DateFrom,
            request.DateTo);

        return Ok(result);
    }
}

public record MT5LiveImportRequestDto(DateTime DateFrom, DateTime DateTo);

public record MT5SyncRequestDto(DateTime DateFrom, DateTime DateTo);
