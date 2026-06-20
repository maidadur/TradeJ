using Microsoft.AspNetCore.Mvc;

namespace TradeJ.Controllers;

/// <summary>
/// Proxies Yahoo Finance chart API requests to avoid browser CORS restrictions.
/// Frontend calls /yf/... which nginx rewrites to /api/yf/... in production.
/// </summary>
[ApiController]
[Route("api/yf")]
public class YahooFinanceProxyController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private static readonly Uri YahooBase = new("https://query1.finance.yahoo.com/");

    [HttpGet("v8/finance/chart/{ticker}")]
    public async Task<IActionResult> Chart(
        string ticker,
        [FromQuery] string interval,
        [FromQuery] string? range = null,
        [FromQuery] long? period1 = null,
        [FromQuery] long? period2 = null,
        [FromQuery] bool includePrePost = false,
        CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("yahoo");
        var path = $"v8/finance/chart/{Uri.EscapeDataString(ticker)}?interval={interval}";
        path += period1.HasValue && period2.HasValue
            ? $"&period1={period1}&period2={period2}"
            : $"&range={range}";
        path += $"&includePrePost={includePrePost.ToString().ToLower()}";

        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(YahooBase, path));
        req.Headers.Add("User-Agent", "Mozilla/5.0");
        req.Headers.Add("Accept", "application/json");

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        return Content(body, "application/json");
    }
}
