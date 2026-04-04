using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Services;

/// <summary>
/// Parses ByBit Closed P&amp;L CSV export.
/// Supported columns (flexible matching):
///   Contract/Symbol, Side, Avg Entry Price, Avg Exit/Close Price,
///   Closed P&amp;L / Realized PnL, Qty, Fill Time / Created Time
/// </summary>
public class ByBitImportService(AppDbContext db) : IImportService
{
    public async Task<ImportResultDto> ImportAsync(int accountId, Stream fileStream, string fileName)
    {
        var imported = 0;
        var skipped  = 0;
        var errors   = new List<string>();

        using var reader = new StreamReader(fileStream);
        var content = await reader.ReadToEndAsync();

        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return new ImportResultDto(0, 0, 1, ["File is empty or has no data rows."]);

        var delimiter = lines[0].Contains('\t') ? '\t' : ',';

        var headers = SplitLine(lines[0], delimiter)
            .Select(h => h.Trim('"').Trim().ToLowerInvariant()
                .Replace(" ", "").Replace("&", "").Replace("/", "").Replace(".", ""))
            .ToArray();

        var orderIdIdx     = FindCol(headers, "orderid", "orderno", "tradeid");
        var symbolIdx      = FindCol(headers, "symbol", "contract");
        var sideIdx        = FindCol(headers, "side", "direction");
        var qtyIdx         = FindCol(headers, "qty", "quantity", "closedsize", "size");
        var entryPriceIdx  = FindCol(headers, "avgentryprice", "openprice", "entrprice");
        var exitPriceIdx   = FindCol(headers, "avgexitprice", "avgcloseprice", "closeprice");
        var pnlIdx         = FindCol(headers, "closedpnl", "realizedpnl", "pnl", "profit");
        var feeIdx         = FindCol(headers, "tradingfee", "closefee", "fee", "openfee");
        var timeIdx        = FindCol(headers, "filledtime", "createtime", "updatedtime", "createdtime", "tradetime");

        if (symbolIdx is null || sideIdx is null || pnlIdx is null)
            return new ImportResultDto(0, 0, 1,
                ["Unrecognized file format. Expected ByBit Closed P&L export with columns: Symbol/Contract, Side, Closed P&L."]);

        var existingIds = await db.Trades
            .Where(t => t.AccountId == accountId)
            .Select(t => t.BrokerTradeId)
            .ToHashSetAsync();

        var newTrades = new List<Trade>();

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var cols = SplitLine(lines[i], delimiter);
                if (cols.Length < 3) continue;

                string Get(int? idx) => idx.HasValue && idx.Value < cols.Length ? cols[idx.Value].Trim().Trim('"') : "";

                var symbol = Get(symbolIdx);
                if (string.IsNullOrEmpty(symbol)) { skipped++; continue; }

                // Generate a deterministic trade ID if no order ID column
                var orderId = orderIdIdx.HasValue ? Get(orderIdIdx) : $"{symbol}_{i}_{DateTime.UtcNow.Ticks}";
                var brokerTradeId = "bb_" + orderId;

                if (existingIds.Contains(brokerTradeId)) { skipped++; continue; }

                var sideRaw = Get(sideIdx).ToLowerInvariant();
                var direction = sideRaw == "buy" || sideRaw == "long"
                    ? TradeDirection.Long : TradeDirection.Short;

                decimal D(int? idx) { decimal.TryParse(Get(idx), NumberStyles.Any, CultureInfo.InvariantCulture, out var v); return v; }

                var closedPnL  = D(pnlIdx);
                var fee        = D(feeIdx);
                var entryPrice = D(entryPriceIdx);
                var exitPrice  = D(exitPriceIdx);
                var qty        = D(qtyIdx);

                var tradeTime = ParseByBitDate(Get(timeIdx));

                var trade = new Trade
                {
                    AccountId     = accountId,
                    BrokerTradeId = brokerTradeId,
                    Symbol        = symbol,
                    Direction     = direction,
                    Status        = TradeStatus.Closed,
                    EntryPrice    = entryPrice,
                    ExitPrice     = exitPrice,
                    EntryTime     = tradeTime,
                    ExitTime      = tradeTime,
                    Volume        = qty,
                    GrossPnL      = closedPnL,
                    Commission    = -Math.Abs(fee),
                    Swap          = 0,
                    NetPnL        = closedPnL - Math.Abs(fee),
                    ImportedAt    = DateTime.UtcNow
                };

                newTrades.Add(trade);
                existingIds.Add(brokerTradeId);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row {i + 1}: {ex.Message}");
            }
        }

        if (newTrades.Count > 0)
        {
            db.Trades.AddRange(newTrades);
            await db.SaveChangesAsync();
        }

        return new ImportResultDto(imported, skipped, errors.Count, errors);
    }

    private static int? FindCol(string[] headers, params string[] names)
    {
        foreach (var n in names)
        {
            var i = Array.IndexOf(headers, n);
            if (i >= 0) return i;
        }
        return null;
    }

    private static DateTime ParseByBitDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DateTime.UtcNow;
        // ByBit uses formats like "2024-01-15 10:00:00" or Unix timestamp
        if (long.TryParse(value, out var unix))
            return DateTimeOffset.FromUnixTimeMilliseconds(unix).UtcDateTime;
        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }

    private static string[] SplitLine(string line, char delimiter)
    {
        var result = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                { cur.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (ch == delimiter && !inQuotes)
            { result.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(ch);
        }
        result.Add(cur.ToString());
        return [.. result];
    }
}
