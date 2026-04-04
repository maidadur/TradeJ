using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Services;

/// <summary>
/// Parses cTrader Position History CSV export.
/// Expected columns: Position ID, Symbol, Direction, Volume (Lots),
///   Open Time, Open Price, Close Time, Close Price,
///   Commission, Swap, Gross Profit, Net Profit, Close Reason
/// </summary>
public class CTraderImportService(AppDbContext db) : IImportService
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
            .Select(h => h.Trim('"').Trim().ToLowerInvariant().Replace(" ", "").Replace("(", "").Replace(")", ""))
            .ToArray();

        var positionIdIdx  = FindCol(headers, "positionid", "positionid");
        var symbolIdx      = FindCol(headers, "symbol");
        var directionIdx   = FindCol(headers, "direction", "side");
        var volumeIdx      = FindCol(headers, "volumelots", "volume", "size");
        var openTimeIdx    = FindCol(headers, "opentime");
        var openPriceIdx   = FindCol(headers, "openprice");
        var closeTimeIdx   = FindCol(headers, "closetime");
        var closePriceIdx  = FindCol(headers, "closeprice");
        var commissionIdx  = FindCol(headers, "commission");
        var swapIdx        = FindCol(headers, "swap");
        var grossPnLIdx    = FindCol(headers, "grossprofit");
        var netPnLIdx      = FindCol(headers, "netprofit");

        if (positionIdIdx is null || symbolIdx is null || directionIdx is null)
            return new ImportResultDto(0, 0, 1,
                ["Unrecognized file format. Expected cTrader position history export with columns: Position ID, Symbol, Direction."]);

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

                var posId = Get(positionIdIdx);
                if (string.IsNullOrEmpty(posId)) { skipped++; continue; }
                if (existingIds.Contains("ct_" + posId)) { skipped++; continue; }

                var dirRaw = Get(directionIdx).ToLowerInvariant();
                var direction = dirRaw.StartsWith("buy") || dirRaw == "long"
                    ? TradeDirection.Long : TradeDirection.Short;

                decimal D(int? idx) { decimal.TryParse(Get(idx), NumberStyles.Any, CultureInfo.InvariantCulture, out var v); return v; }

                var closeTimeStr = Get(closeTimeIdx);
                var status = string.IsNullOrEmpty(closeTimeStr) ? TradeStatus.Open : TradeStatus.Closed;

                var grossPnL   = D(grossPnLIdx);
                var netPnL     = D(netPnLIdx);
                var commission = D(commissionIdx);
                var swap       = D(swapIdx);

                // If gross P&L columns not present, derive from net
                if (grossPnLIdx is null) grossPnL = netPnL - commission - swap;

                var trade = new Trade
                {
                    AccountId     = accountId,
                    BrokerTradeId = "ct_" + posId,
                    Symbol        = Get(symbolIdx),
                    Direction     = direction,
                    Status        = status,
                    EntryPrice    = D(openPriceIdx),
                    ExitPrice     = status == TradeStatus.Closed ? D(closePriceIdx) : null,
                    EntryTime     = ParseDate(Get(openTimeIdx)),
                    ExitTime      = status == TradeStatus.Closed ? ParseDate(closeTimeStr) : null,
                    Volume        = D(volumeIdx),
                    GrossPnL      = grossPnL,
                    Commission    = commission,
                    Swap          = swap,
                    NetPnL        = netPnL,
                    ImportedAt    = DateTime.UtcNow
                };

                newTrades.Add(trade);
                existingIds.Add("ct_" + posId);
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

    private static DateTime ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DateTime.UtcNow;
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
