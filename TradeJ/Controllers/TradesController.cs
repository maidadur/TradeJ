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
        t.Notes,
        t.Tags,
        t.ImportedAt,
        t.TradeTags?.Select(tt => tt.TagId).ToList() ?? [],
        t.TradeStrategies?.Select(ts => ts.StrategyId).ToList() ?? []
    );
}
