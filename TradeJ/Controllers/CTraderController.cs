using Microsoft.AspNetCore.Mvc;
using TradeJ.DTOs;
using TradeJ.Services;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/ctrader")]
public class CTraderController(CTraderApiService ctrader) : ControllerBase
{
    /// <summary>
    /// Returns the cTrader OAuth authorization URL that the frontend popup should open.
    /// </summary>
    [HttpGet("oauth-url")]
    public ActionResult<CTraderOAuthUrlResponse> GetOAuthUrl()
    {
        try
        {
            var url = ctrader.GetOAuthUrl();
            return Ok(new CTraderOAuthUrlResponse(url));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Exchange OAuth code for an access token and return all cTrader accounts
    /// that have been authorized for this application.
    /// </summary>
    [HttpPost("accounts")]
    public async Task<ActionResult<CTraderAccountsResponse>> GetAccounts(
        [FromBody] CTraderExchangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "Authorization code is required." });

        try
        {
            var result = await ctrader.ExchangeAndListAccountsAsync(request.Code);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Import closed positions from one cTrader account into a TradeJ account.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportResultDto>> Import(
        [FromBody] CTraderImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
            return BadRequest(new { message = "Access token is required." });

        try
        {
            var result = await ctrader.ImportAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Persist cTrader credentials on a TradeJ account so auto-sync can run
    /// without requiring another browser login.
    /// </summary>
    [HttpPost("link")]
    public async Task<IActionResult> LinkAccount([FromBody] CTraderLinkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { message = "Refresh token is required." });

        try
        {
            await ctrader.LinkAccountAsync(request);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

