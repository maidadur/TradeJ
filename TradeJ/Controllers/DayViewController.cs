using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DayViewController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DayViewDto>> Get(
        [FromQuery] int accountId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo)
    {
        var query = db.Trades
            .Where(t => t.AccountId == accountId && t.Status == TradeStatus.Closed);

        if (dateFrom.HasValue)
            query = query.Where(t => t.ExitTime >= dateFrom.Value);
        else if (year.HasValue)
            query = query.Where(t => t.ExitTime!.Value.Year == year.Value);

        if (dateTo.HasValue)
            query = query.Where(t => t.ExitTime <= dateTo.Value);
        else if (year.HasValue && month.HasValue)
            query = query.Where(t => t.ExitTime!.Value.Month == month.Value);

        var trades = await query
            .OrderBy(t => t.ExitTime)
            .ToListAsync();

        var dateStrings = trades
            .Select(t => DateOnly.FromDateTime(t.ExitTime!.Value.Date))
            .Distinct()
            .ToList();

        var notesMap = await db.DayNotes
            .Where(n => n.AccountId == accountId && dateStrings.Contains(n.Date))
            .ToDictionaryAsync(n => n.Date, n => n.Content);

        var groups = trades
            .GroupBy(t => t.ExitTime!.Value.Date)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var date      = g.Key;
                var dateOnly  = DateOnly.FromDateTime(date);
                var tradeList = g.OrderBy(t => t.EntryTime).ToList();
                var wins      = tradeList.Count(t => t.NetPnL > 0);
                var losses    = tradeList.Count(t => t.NetPnL < 0);
                var grossPnL  = tradeList.Sum(t => t.GrossPnL);
                var netPnL    = tradeList.Sum(t => t.NetPnL);
                var commission= tradeList.Sum(t => t.Commission);
                var swap      = tradeList.Sum(t => t.Swap);
                var volume    = tradeList.Sum(t => t.Volume);
                var totalLoss = Math.Abs(tradeList.Where(t => t.NetPnL < 0).Sum(t => t.NetPnL));
                var totalWin  = tradeList.Where(t => t.NetPnL > 0).Sum(t => t.NetPnL);
                var pf        = totalLoss == 0 ? (totalWin > 0 ? 99.99m : 0m)
                                               : Math.Round(totalWin / totalLoss, 2);
                var winRate   = tradeList.Count > 0
                    ? Math.Round((decimal)wins / tradeList.Count * 100, 2)
                    : 0m;
                var weekNum   = ISOWeek.GetWeekOfYear(date);

                return new DayGroup(
                    Date:     date.ToString("yyyy-MM-dd"),
                    DayLabel: date.ToString("ddd, MMM dd, yyyy", CultureInfo.GetCultureInfo("en-US")),
                    WeekNumber: weekNum,
                    Stats: new DayStats(
                        TotalTrades: tradeList.Count,
                        Winners:     wins,
                        Losers:      losses,
                        GrossPnL:    Math.Round(grossPnL, 2),
                        NetPnL:      Math.Round(netPnL, 2),
                        Commission:  Math.Round(commission, 2),
                        Swap:        Math.Round(swap, 2),
                        Volume:      Math.Round(volume, 2),
                        WinRate:     winRate,
                        ProfitFactor: pf),
                    Trades: tradeList.Select(t => new DayTradeItem(
                        Id:              t.Id,
                        Symbol:          t.Symbol,
                        Direction:       t.Direction.ToString(),
                        Status:          t.Status.ToString(),
                        EntryTime:       t.EntryTime,
                        ExitTime:        t.ExitTime,
                        EntryPrice:      t.EntryPrice,
                        ExitPrice:       t.ExitPrice,
                        Volume:          t.Volume,
                        GrossPnL:        t.GrossPnL,
                        Commission:      t.Commission,
                        Swap:            t.Swap,
                        NetPnL:          t.NetPnL,
                        Tags:            t.Tags,
                        DurationMinutes: t.ExitTime.HasValue
                            ? (int)(t.ExitTime.Value - t.EntryTime).TotalMinutes
                            : 0
                    )).ToList(),
                    Note: notesMap.TryGetValue(dateOnly, out var noteContent) ? noteContent : null
                );
            })
            .ToList();

        return Ok(new DayViewDto(groups));
    }
}
