using Microsoft.AspNetCore.Mvc;
using TradeJ.Data;
using TradeJ.Services;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BarsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int accountId,
        [FromQuery] string symbol,
        [FromQuery] string timeframe,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromServices] MT5BarsService barsService)
    {
        var account = await db.Accounts.FindAsync(accountId);
        if (account is null)
            return BadRequest(new { message = $"Account {accountId} not found." });

        if (string.IsNullOrWhiteSpace(account.MT5Server) || string.IsNullOrWhiteSpace(account.MT5InvestorPassword))
            return BadRequest(new { message = "Chart requires the local MT5 bridge — configure MT5 Server + Investor Password for this account and run scripts/mt5_bridge.py." });

        var result = await barsService.GetBarsAsync(
            account.AccountNumber, account.MT5InvestorPassword, account.MT5Server,
            symbol, timeframe, from, to);

        if (result.Error is not null)
            return BadRequest(new { message = result.Error });

        return Ok(result.Bars);
    }
}
