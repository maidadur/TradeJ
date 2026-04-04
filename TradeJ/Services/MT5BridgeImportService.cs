using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Services;

/// <summary>
/// Fetches MT5 deal history from a locally running Python bridge (mt5_bridge.py).
/// The bridge must be started once by the user: python scripts/mt5_bridge.py
/// Investor password is stored on the Account — never transmitted back to callers.
/// </summary>
public class MT5BridgeImportService(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config)
{
    private string BridgeUrl => config["MT5Bridge:Url"] ?? "http://localhost:8765";

    public async Task<ImportResultDto> ImportAsync(
        int accountId,
        string login,
        string password,
        string server,
        DateTime dateFrom,
        DateTime dateTo)
    {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(120); // MT5 connection can be slow

        var from = Uri.EscapeDataString(dateFrom.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
        var to   = Uri.EscapeDataString(dateTo.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
        var url  = $"{BridgeUrl}/deals?login={Uri.EscapeDataString(login)}&password={Uri.EscapeDataString(password)}&server={Uri.EscapeDataString(server)}&from={from}&to={to}";

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url);
        }
        catch (HttpRequestException ex)
        {
            return new ImportResultDto(0, 0, 1,
                [$"Cannot connect to MT5 bridge at {BridgeUrl}. Make sure you have started 'python scripts/mt5_bridge.py'. Error: {ex.Message}"]);
        }
        catch (TaskCanceledException)
        {
            return new ImportResultDto(0, 0, 1,
                ["MT5 bridge request timed out (120 s). Check that MT5 terminal is running and accepting connections."]);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return new ImportResultDto(0, 0, 1,
                [$"Bridge error {(int)response.StatusCode}: {body[..Math.Min(body.Length, 300)]}"]);
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var deals   = await response.Content.ReadFromJsonAsync<List<MetaApiDeal>>(options) ?? [];

        return await ProcessDeals(accountId, deals);
    }

    private async Task<ImportResultDto> ProcessDeals(int accountId, List<MetaApiDeal> deals)
    {
        int imported = 0, skipped = 0, errors = 0;
        var errorMessages = new List<string>();

        var positions = deals
            .Where(d => d.Type is "DEAL_TYPE_BUY" or "DEAL_TYPE_SELL"
                     && d.Entry is "DEAL_ENTRY_IN" or "DEAL_ENTRY_OUT" or "DEAL_ENTRY_INOUT"
                     && d.PositionId is not null)
            .GroupBy(d => d.PositionId!)
            .ToList();

        foreach (var group in positions)
        {
            try
            {
                var positionId = group.Key;
                var entryDeals = group.Where(d => d.Entry == "DEAL_ENTRY_IN").OrderBy(d => d.Time).ToList();
                var exitDeals  = group.Where(d => d.Entry is "DEAL_ENTRY_OUT" or "DEAL_ENTRY_INOUT").OrderBy(d => d.Time).ToList();

                if (entryDeals.Count == 0) { skipped++; continue; }

                var firstEntry = entryDeals.First();
                var lastExit   = exitDeals.LastOrDefault();
                var direction  = firstEntry.Type == "DEAL_TYPE_BUY" ? TradeDirection.Long : TradeDirection.Short;

                // Aggregate all deals — handles partial closes and scale-ins correctly
                var totalEntryVol = entryDeals.Sum(d => d.Volume);
                var entryPrice    = totalEntryVol > 0
                    ? entryDeals.Sum(d => d.Price * d.Volume) / totalEntryVol
                    : firstEntry.Price;
                var totalExitVol  = exitDeals.Sum(d => d.Volume);
                var exitPrice     = totalExitVol > 0 && lastExit is not null
                    ? exitDeals.Sum(d => d.Price * d.Volume) / totalExitVol
                    : lastExit?.Price;

                var commission = group.Sum(d => d.Commission);
                var swap       = group.Sum(d => d.Swap);
                var grossPnL   = exitDeals.Sum(d => d.Profit);
                var netPnL     = grossPnL + commission + swap;
                var status     = lastExit is not null ? TradeStatus.Closed : TradeStatus.Open;

                var existing = await db.Trades.FirstOrDefaultAsync(
                    t => t.AccountId == accountId && t.BrokerTradeId == positionId);

                if (existing is not null)
                {
                    // Update in case new partial closes have since been added
                    existing.ExitTime   = lastExit?.Time;
                    existing.ExitPrice  = exitPrice;
                    existing.Volume     = totalEntryVol;
                    existing.GrossPnL   = grossPnL;
                    existing.Commission = commission;
                    existing.Swap       = swap;
                    existing.NetPnL     = netPnL;
                    existing.Status     = status;
                    skipped++;
                }
                else
                {
                    db.Trades.Add(new Trade
                    {
                        AccountId     = accountId,
                        BrokerTradeId = positionId,
                        Symbol        = firstEntry.Symbol ?? "UNKNOWN",
                        Direction     = direction,
                        Status        = status,
                        EntryTime     = firstEntry.Time,
                        ExitTime      = lastExit?.Time,
                        EntryPrice    = entryPrice,
                        ExitPrice     = exitPrice,
                        Volume        = totalEntryVol,
                        GrossPnL      = grossPnL,
                        Commission    = commission,
                        Swap          = swap,
                        NetPnL        = netPnL
                    });
                    imported++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                errorMessages.Add($"Position {group.Key}: {ex.Message}");
            }
        }

        await db.SaveChangesAsync();
        return new ImportResultDto(imported, skipped, errors, errorMessages);
    }
}
