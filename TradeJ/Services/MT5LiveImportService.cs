using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Services;

public class MT5LiveImportService(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config)
{
    private string BaseUrl(string region) =>
        config["MetaApi:BaseUrl"] ?? $"https://mt-client-api-v1.{region}.agiliumtrade.ai";

    public async Task<ImportResultDto> ImportAsync(
        int accountId,
        string metaApiToken,
        string metaApiAccountId,
        string region,
        DateTime dateFrom,
        DateTime dateTo)
    {
        var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Add("auth-token", metaApiToken);

        var start = Uri.EscapeDataString(dateFrom.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        var end   = Uri.EscapeDataString(dateTo.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        var url   = $"{BaseUrl(region)}/users/current/accounts/{metaApiAccountId}/history-deals/time/{start}/{end}";

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url);
        }
        catch (HttpRequestException ex)
        {
            return new ImportResultDto(0, 0, 1, [$"Connection failed: {ex.Message}"]);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return new ImportResultDto(0, 0, 1, [$"MetaApi {(int)response.StatusCode}: {body[..Math.Min(body.Length, 300)]}"]);
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var deals   = await response.Content.ReadFromJsonAsync<List<MetaApiDeal>>(options) ?? [];

        return await ProcessDeals(accountId, deals);
    }

    private async Task<ImportResultDto> ProcessDeals(int accountId, List<MetaApiDeal> deals)
    {
        int imported = 0, skipped = 0, errors = 0;
        var errorMessages = new List<string>();

        // Only buy/sell deals, skip balance/credit/etc.
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

// MetaApi REST response models
public class MetaApiDeal
{
    public string Id { get; set; } = "";
    public string? PositionId { get; set; }
    public string Type { get; set; } = "";       // DEAL_TYPE_BUY | DEAL_TYPE_SELL | ...
    public string Entry { get; set; } = "";      // DEAL_ENTRY_IN | DEAL_ENTRY_OUT | ...
    public string? Symbol { get; set; }
    public DateTime Time { get; set; }
    public decimal Volume { get; set; }
    public decimal Price { get; set; }
    public decimal Profit { get; set; }
    public decimal Commission { get; set; }
    public decimal Swap { get; set; }
    public string? Comment { get; set; }
}
