using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Services;

/// <summary>
/// Parses MetaTrader 5 History CSV export.
/// Supported format: Ticket, Open Time, Type, Size, Symbol, Open Price,
///   S / L, T / P, Close Time, Close Price, Commission, Taxes, Swap, Profit, Comment
/// </summary>
public class MT5ImportService(AppDbContext db) : IImportService
{
    public async Task<ImportResultDto> ImportAsync(int accountId, Stream fileStream, string fileName)
    {
        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        using var reader = new StreamReader(fileStream);
        var content = await reader.ReadToEndAsync();

        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return new ImportResultDto(0, 0, 1, ["File is empty or has no data rows."]);

        // Detect delimiter: prefer tab if it appears before comma in the header line
        var delimiter = lines[0].Contains('\t') ? '\t' : ',';

        var headers = SplitLine(lines[0], delimiter)
            .Select(h => NormalizeHeader(h))
            .ToArray();

        var ticketIdx    = FindCol(headers, "ticket", "#");
        var openTimeIdx  = FindCol(headers, "opentime", "time");
        var typeIdx      = FindCol(headers, "type");
        var sizeIdx      = FindCol(headers, "size", "volume");
        var symbolIdx    = FindCol(headers, "symbol");
        var openPriceIdx = FindCol(headers, "openprice");
        var closeTimeIdx = FindCol(headers, "closetime");
        var closePriceIdx = FindCol(headers, "closeprice");
        var commIdx      = FindCol(headers, "commission");
        var swapIdx      = FindCol(headers, "swap");
        var profitIdx    = FindCol(headers, "profit");

        if (ticketIdx is null || openTimeIdx is null || typeIdx is null || symbolIdx is null)
            return new ImportResultDto(0, 0, 1,
                ["Unrecognized file format. Expected MT5 account history export with columns: Ticket, Open Time, Type, Symbol."]);

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

                string Get(int? idx) => idx.HasValue && idx.Value < cols.Length ? cols[idx.Value].Trim() : "";

                var type = Get(typeIdx).ToLowerInvariant();
                if (type != "buy" && type != "sell") { skipped++; continue; }

                var ticket = Get(ticketIdx);
                if (string.IsNullOrEmpty(ticket)) { skipped++; continue; }

                if (existingIds.Contains(ticket)) { skipped++; continue; }

                var closeTimeStr = Get(closeTimeIdx);
                var status = string.IsNullOrEmpty(closeTimeStr) ? TradeStatus.Open : TradeStatus.Closed;

                decimal D(int? idx) { decimal.TryParse(Get(idx), NumberStyles.Any, CultureInfo.InvariantCulture, out var v); return v; }

                var grossPnL   = D(profitIdx);
                var commission = D(commIdx);
                var swap       = D(swapIdx);
                var entryPrice = D(openPriceIdx);
                var exitPrice  = D(closePriceIdx);

                var trade = new Trade
                {
                    AccountId    = accountId,
                    BrokerTradeId = ticket,
                    Symbol       = Get(symbolIdx),
                    Direction    = type == "buy" ? TradeDirection.Long : TradeDirection.Short,
                    Status       = status,
                    EntryPrice   = entryPrice,
                    ExitPrice    = status == TradeStatus.Closed ? exitPrice : null,
                    EntryTime    = ParseMT5Date(Get(openTimeIdx)),
                    ExitTime     = status == TradeStatus.Closed ? ParseMT5Date(closeTimeStr) : null,
                    Volume       = D(sizeIdx),
                    GrossPnL     = grossPnL,
                    Commission   = commission,
                    Swap         = swap,
                    NetPnL       = grossPnL + commission + swap,
                    ImportedAt   = DateTime.UtcNow
                };

                newTrades.Add(trade);
                existingIds.Add(ticket);
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

    private static string NormalizeHeader(string h) =>
        h.Trim('"').Trim().ToLowerInvariant()
         .Replace(" ", "").Replace("/", "").Replace("\\", "");

    private static int? FindCol(string[] headers, params string[] names)
    {
        foreach (var n in names)
        {
            var i = Array.IndexOf(headers, n);
            if (i >= 0) return i;
        }
        return null;
    }

    private static DateTime ParseMT5Date(string value)
    {
        if (DateTime.TryParseExact(value, "yyyy.MM.dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
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
