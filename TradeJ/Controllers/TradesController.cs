using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TradesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TradeDto>>> GetAll(
        [FromQuery] int[] accountIds,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? symbol = null,
        [FromQuery] string? direction = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? sortBy = "entryTime",
        [FromQuery] bool sortDesc = false)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 500) pageSize = 50;

        var query = db.Trades
            .Include(t => t.Account)
            .Include(t => t.TradeTags)
            .Include(t => t.TradeStrategies)
            .Where(t => accountIds.Contains(t.AccountId));

        if (!string.IsNullOrWhiteSpace(symbol))
            query = query.Where(t => t.Symbol.ToLower().Contains(symbol.ToLower()));

        if (!string.IsNullOrWhiteSpace(direction) &&
            Enum.TryParse<Models.TradeDirection>(direction, true, out var dir))
            query = query.Where(t => t.Direction == dir);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<Models.TradeStatus>(status, true, out var st))
            query = query.Where(t => t.Status == st);

        if (dateFrom.HasValue)
            query = query.Where(t => t.EntryTime >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(t => t.EntryTime <= dateTo.Value);

        query = (sortBy?.ToLower(), sortDesc) switch
        {
            ("entrytime", false)  => query.OrderBy(t => t.EntryTime),
            ("entrytime", true)   => query.OrderByDescending(t => t.EntryTime),
            ("netpnl", false)     => query.OrderBy(t => t.NetPnL),
            ("netpnl", true)      => query.OrderByDescending(t => t.NetPnL),
            ("symbol", false)     => query.OrderBy(t => t.Symbol),
            ("symbol", true)      => query.OrderByDescending(t => t.Symbol),
            _                     => query.OrderByDescending(t => t.EntryTime)
        };

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => MapToDto(t, baseUrl))
            .ToListAsync();

        return Ok(new PagedResult<TradeDto>(items, totalCount, page, pageSize, totalPages));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TradeDto>> GetById(int id)
    {
        var t = await db.Trades
            .Include(t => t.Account)
            .Include(t => t.TradeTags)
            .Include(t => t.TradeStrategies)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (t is null) return NotFound();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(MapToDto(t, baseUrl));
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] int[] accountIds,
        [FromQuery] string? symbol = null,
        [FromQuery] string? direction = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? sortBy = "entryTime",
        [FromQuery] bool sortDesc = true)
    {
        var query = db.Trades
            .Include(t => t.Account)
            .Include(t => t.TradeTags).ThenInclude(tt => tt.Tag)
            .Include(t => t.TradeStrategies).ThenInclude(ts => ts.Strategy)
            .AsQueryable();

        if (accountIds.Length > 0)
            query = query.Where(t => accountIds.Contains(t.AccountId));

        if (!string.IsNullOrWhiteSpace(symbol))
            query = query.Where(t => t.Symbol.ToLower().Contains(symbol.ToLower()));

        if (!string.IsNullOrWhiteSpace(direction) &&
            Enum.TryParse<Models.TradeDirection>(direction, true, out var dir))
            query = query.Where(t => t.Direction == dir);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<Models.TradeStatus>(status, true, out var st))
            query = query.Where(t => t.Status == st);

        if (dateFrom.HasValue)
            query = query.Where(t => t.EntryTime >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(t => t.EntryTime <= dateTo.Value);

        query = (sortBy?.ToLower(), sortDesc) switch
        {
            ("entrytime", false) => query.OrderBy(t => t.EntryTime),
            ("entrytime", true)  => query.OrderByDescending(t => t.EntryTime),
            ("netpnl", false)    => query.OrderBy(t => t.NetPnL),
            ("netpnl", true)     => query.OrderByDescending(t => t.NetPnL),
            ("symbol", false)    => query.OrderBy(t => t.Symbol),
            ("symbol", true)     => query.OrderByDescending(t => t.Symbol),
            _                    => query.OrderByDescending(t => t.EntryTime)
        };

        var trades = await query.ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Id,AccountId,AccountName,BrokerTradeId,Symbol,Direction,Status," +
                      "EntryTime,ExitTime,EntryPrice,ExitPrice,Volume," +
                      "GrossPnL,Commission,Swap,NetPnL," +
                      "Tags,Strategies,Notes,ImportedAt");

        foreach (var t in trades)
        {
            var tagNames  = t.TradeTags?.Select(tt => tt.Tag?.Name ?? "").Where(n => n != "").ToList() ?? [];
            var stratNames = t.TradeStrategies?.Select(ts => ts.Strategy?.Name ?? "").Where(n => n != "").ToList() ?? [];
            var notes = StripHtml(t.Notes ?? "");

            sb.AppendLine(string.Join(",", [
                CsvField(t.Id.ToString()),
                CsvField(t.AccountId.ToString()),
                CsvField(t.Account?.Name ?? ""),
                CsvField(t.BrokerTradeId),
                CsvField(t.Symbol),
                CsvField(t.Direction.ToString()),
                CsvField(t.Status.ToString()),
                CsvField(t.EntryTime.ToString("yyyy-MM-dd HH:mm:ss")),
                CsvField(t.ExitTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""),
                CsvField(t.EntryPrice.ToString("F8")),
                CsvField(t.ExitPrice?.ToString("F8") ?? ""),
                CsvField(t.Volume.ToString("F8")),
                CsvField(t.GrossPnL.ToString("F2")),
                CsvField(t.Commission.ToString("F2")),
                CsvField(t.Swap.ToString("F2")),
                CsvField(t.NetPnL.ToString("F2")),
                CsvField(string.Join("; ", tagNames)),
                CsvField(string.Join("; ", stratNames)),
                CsvField(notes),
                CsvField(t.ImportedAt.ToString("yyyy-MM-dd HH:mm:ss"))
            ]));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"trades_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }

    private static string CsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        // Remove script/style blocks
        html = Regex.Replace(html, @"<(script|style)[^>]*>.*?</(script|style)>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        // Remove img tags (screenshots)
        html = Regex.Replace(html, @"<img[^>]*>", "", RegexOptions.IgnoreCase);
        // Replace block tags with newlines
        html = Regex.Replace(html, @"<(br|p|div|li|tr|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        // Remove remaining tags
        html = Regex.Replace(html, @"<[^>]+>", "", RegexOptions.IgnoreCase);
        // Decode common HTML entities
        html = html.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
                   .Replace("&nbsp;", " ").Replace("&quot;", "\"").Replace("&#39;", "'");
        // Collapse excessive whitespace
        html = Regex.Replace(html, @"[ \t]+", " ");
        html = Regex.Replace(html, @"\n{3,}", "\n\n");
        return html.Trim();
    }

    [HttpPatch("{id:int}/strategies")]
    public async Task<IActionResult> UpdateStrategies(int id, [FromBody] UpdateTradeStrategiesDto dto)
    {
        var trade = await db.Trades
            .Include(t => t.TradeStrategies)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (trade is null) return NotFound();

        db.TradeStrategies.RemoveRange(trade.TradeStrategies);
        foreach (var stratId in dto.StrategyIds)
            db.TradeStrategies.Add(new TradeStrategy { TradeId = id, StrategyId = stratId });

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/tags")]
    public async Task<IActionResult> UpdateTags(int id, [FromBody] UpdateTradeTagsDto dto)
    {
        var trade = await db.Trades
            .Include(t => t.TradeTags)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (trade is null) return NotFound();

        db.TradeTags.RemoveRange(trade.TradeTags);
        foreach (var tagId in dto.TagIds)
            db.TradeTags.Add(new TradeTag { TradeId = id, TagId = tagId });

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/notes")]
    public async Task<IActionResult> UpdateNotes(int id, [FromBody] UpdateTradeNotesDto dto)
    {
        var trade = await db.Trades.FindAsync(id);
        if (trade is null) return NotFound();

        trade.Notes = dto.Notes;
        trade.Tags  = dto.Tags;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/metrics")]
    public async Task<IActionResult> UpdateMetrics(int id, [FromBody] UpdateTradeMetricsDto dto)
    {
        var trade = await db.Trades.FindAsync(id);
        if (trade is null) return NotFound();

        trade.RR           = dto.RR;
        trade.ActualRR     = dto.ActualRR;
        trade.RiskPercent  = dto.RiskPercent;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static TradeDto MapToDto(Models.Trade t, string baseUrl) => new(
        t.Id,
        t.AccountId,
        t.Account.Name,
        t.BrokerTradeId,
        t.Symbol,
        t.Direction.ToString(),
        t.Status.ToString(),
        t.EntryPrice,
        t.ExitPrice,
        t.EntryTime,
        t.ExitTime,
        t.Volume,
        t.GrossPnL,
        t.Commission,
        t.Swap,
        t.NetPnL,
        t.RR,
        t.ActualRR,
        t.RiskPercent,
        t.Notes,
        t.Tags,
        t.ImportedAt,
        t.TradeTags?.Select(tt => tt.TagId).ToList() ?? [],
        t.TradeStrategies?.Select(ts => ts.StrategyId).ToList() ?? []
    );
}
