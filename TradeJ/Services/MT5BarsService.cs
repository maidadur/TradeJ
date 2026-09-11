using System.Text.Json;
using TradeJ.DTOs;

namespace TradeJ.Services;

/// <summary>
/// Fetches OHLC bars from the locally running Python bridge (mt5_bridge.py) for the price chart.
/// </summary>
public class MT5BarsService(IHttpClientFactory httpClientFactory, IConfiguration config)
{
    private string BridgeUrl => config["MT5Bridge:Url"] ?? "http://localhost:8765";

    public async Task<BarsResultDto> GetBarsAsync(
        string login,
        string password,
        string server,
        string symbol,
        string timeframe,
        DateTime dateFrom,
        DateTime dateTo)
    {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(60);

        var from = Uri.EscapeDataString(dateFrom.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss") + "Z");
        var to   = Uri.EscapeDataString(dateTo.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss") + "Z");
        var url  = $"{BridgeUrl}/bars?login={Uri.EscapeDataString(login)}&password={Uri.EscapeDataString(password)}" +
                   $"&server={Uri.EscapeDataString(server)}&symbol={Uri.EscapeDataString(symbol)}" +
                   $"&timeframe={Uri.EscapeDataString(timeframe)}&from={from}&to={to}";

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url);
        }
        catch (HttpRequestException ex)
        {
            return new BarsResultDto([], $"Cannot connect to MT5 bridge at {BridgeUrl}. Make sure you have started 'python scripts/mt5_bridge.py'. Error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return new BarsResultDto([], "MT5 bridge request timed out. Check that MT5 terminal is running and accepting connections.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return new BarsResultDto([], $"Bridge error {(int)response.StatusCode}: {body[..Math.Min(body.Length, 300)]}");
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var bars    = await response.Content.ReadFromJsonAsync<List<BarDto>>(options) ?? [];
        return new BarsResultDto(bars, null);
    }
}

public record BarsResultDto(List<BarDto> Bars, string? Error);
