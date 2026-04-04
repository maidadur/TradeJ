using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StrategiesController(AppDbContext db) : ControllerBase
{
    // GET /api/strategies?accountId=1
    [HttpGet]
    public async Task<ActionResult<List<StrategyListItemDto>>> GetAll([FromQuery] int accountId)
    {
        var strategies = await db.Strategies
            .Where(s => s.AccountId == accountId)
            .Include(s => s.TradeStrategies)
                .ThenInclude(ts => ts.Trade)
            .OrderBy(s => s.Name)
            .ToListAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        return Ok(strategies.Select(s => ToListItem(s, baseUrl)));
    }

    // GET /api/strategies/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<StrategyDetailDto>> GetById(int id)
    {
        var s = await db.Strategies
            .Include(s => s.TradeStrategies)
                .ThenInclude(ts => ts.Trade)
            .Include(s => s.Notes)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (s is null) return NotFound();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(ToDetail(s, baseUrl));
    }

    // POST /api/strategies?accountId=1
    [HttpPost]
    public async Task<ActionResult<StrategyDetailDto>> Create(
        [FromQuery] int accountId,
        [FromBody] CreateStrategyDto dto)
    {
        var strategy = new Strategy
        {
            AccountId   = accountId,
            Name        = dto.Name,
            Description = dto.Description,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return CreatedAtAction(nameof(GetById), new { id = strategy.Id }, ToDetail(strategy, baseUrl));
    }

    // PUT /api/strategies/{id}
    [HttpPut("{id:int}")]
    public async Task<ActionResult<StrategyDetailDto>> Update(int id, [FromBody] UpdateStrategyDto dto)
    {
        var s = await db.Strategies
            .Include(s => s.TradeStrategies).ThenInclude(ts => ts.Trade)
            .Include(s => s.Notes)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (s is null) return NotFound();

        s.Name        = dto.Name;
        s.Description = dto.Description;
        s.UpdatedAt   = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(ToDetail(s, baseUrl));
    }

    // DELETE /api/strategies/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await db.Strategies.FindAsync(id);
        if (s is null) return NotFound();

        db.Strategies.Remove(s);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/strategies/{id}/image
    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> GetImage(int id)
    {
        var s = await db.Strategies
            .Where(x => x.Id == id)
            .Select(x => new { x.ImageData, x.ImageContentType })
            .FirstOrDefaultAsync();

        if (s is null || s.ImageData is null)
            return NotFound();

        return File(s.ImageData, s.ImageContentType ?? "image/png");
    }

    // POST /api/strategies/{id}/image
    [HttpPost("{id:int}/image")]
    public async Task<ActionResult<object>> UploadImage(int id, IFormFile file)
    {
        var s = await db.Strategies.FindAsync(id);
        if (s is null) return NotFound();

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return BadRequest("Unsupported image type.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        s.ImageData        = ms.ToArray();
        s.ImageContentType = file.ContentType;
        s.UpdatedAt        = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var baseUrl  = $"{Request.Scheme}://{Request.Host}";
        var imageUrl = $"{baseUrl}/api/strategies/{id}/image";
        return Ok(new { imageUrl });
    }

    // ---- Helpers ----
    private static StrategyListItemDto ToListItem(Strategy s, string baseUrl)
    {
        var trades = s.TradeStrategies.Select(ts => ts.Trade).ToList();
        var stats  = CalcStats(trades);
        return new StrategyListItemDto(
            s.Id, s.Name, s.Description,
            s.ImageData != null ? $"{baseUrl}/api/strategies/{s.Id}/image" : null,
            stats.TotalTrades, stats.NetPnL, stats.WinRate, stats.ProfitFactor,
            s.CreatedAt);
    }

    private static StrategyDetailDto ToDetail(Strategy s, string baseUrl)
    {
        var trades = s.TradeStrategies.Select(ts => ts.Trade).OrderByDescending(t => t.EntryTime).ToList();
        var stats  = CalcStats(trades);
        return new StrategyDetailDto(
            s.Id, s.AccountId, s.Name, s.Description,
            s.ImageData != null ? $"{baseUrl}/api/strategies/{s.Id}/image" : null,
            stats,
            trades.Select(t => new StrategyTradeDto(
                t.Id, t.Symbol, t.Direction.ToString(), t.Status.ToString(),
                t.EntryTime, t.ExitTime,
                t.EntryPrice, t.ExitPrice, t.Volume, t.NetPnL, t.GrossPnL,
                t.ExitTime.HasValue ? (int)(t.ExitTime.Value - t.EntryTime).TotalMinutes : 0
            )).ToList(),
            s.Notes.OrderByDescending(n => n.UpdatedAt).Select(n => new StrategyNoteDto(
                n.Id, n.Title, n.Content, n.CreatedAt, n.UpdatedAt
            )).ToList(),
            s.CreatedAt, s.UpdatedAt);
    }

    private static StrategyStatsDto CalcStats(List<Trade> trades)
    {
        if (trades.Count == 0)
            return new StrategyStatsDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var wins   = trades.Where(t => t.NetPnL > 0).ToList();
        var losses = trades.Where(t => t.NetPnL < 0).ToList();
        var netPnL = trades.Sum(t => t.NetPnL);
        var grossPnL = trades.Sum(t => t.GrossPnL);
        var commission = trades.Sum(t => t.Commission);
        var totalWin  = wins.Sum(t => t.NetPnL);
        var totalLoss = Math.Abs(losses.Sum(t => t.NetPnL));
        var pf = totalLoss == 0 ? (totalWin > 0 ? 99.99m : 0m) : Math.Round(totalWin / totalLoss, 2);
        var winRate = trades.Count > 0 ? Math.Round((decimal)wins.Count / trades.Count * 100, 2) : 0m;
        var avgHold = trades
            .Where(t => t.ExitTime.HasValue)
            .Select(t => (t.ExitTime!.Value - t.EntryTime).TotalMinutes)
            .DefaultIfEmpty(0)
            .Average();

        return new StrategyStatsDto(
            TotalTrades:    trades.Count,
            Winners:        wins.Count,
            Losers:         losses.Count,
            NetPnL:         Math.Round(netPnL, 2),
            GrossPnL:       Math.Round(grossPnL, 2),
            Commission:     Math.Round(commission, 2),
            WinRate:        winRate,
            ProfitFactor:   pf,
            AvgTradeNetPnL: trades.Count > 0 ? Math.Round(netPnL / trades.Count, 2) : 0m,
            AvgWin:         wins.Count > 0 ? Math.Round(totalWin / wins.Count, 2) : 0m,
            AvgLoss:        losses.Count > 0 ? Math.Round(totalLoss / losses.Count, 2) : 0m,
            AvgHoldMinutes: Math.Round(avgHold, 1)
        );
    }
}
