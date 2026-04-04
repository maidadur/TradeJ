using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Services;

public class DashboardService(AppDbContext db)
{
    public async Task<DashboardDto> GetDashboardAsync(int accountId, int year, int? month)
    {
        var query = db.Trades
            .Where(t => t.AccountId == accountId && t.Status == TradeStatus.Closed);

        if (month.HasValue)
            query = query.Where(t => t.ExitTime!.Value.Year == year && t.ExitTime.Value.Month == month.Value);
        else
            query = query.Where(t => t.ExitTime!.Value.Year == year);

        var trades = await query
            .OrderBy(t => t.ExitTime)
            .ToListAsync();

        var summary  = ComputeSummary(trades);
        var monthly  = month.HasValue ? [] : ComputeMonthly(trades, year);
        var daily    = ComputeDaily(trades, year, month);
        var symbols  = ComputeSymbols(trades);

        return new DashboardDto(summary, monthly, daily, symbols);
    }

    private static DashboardSummary ComputeSummary(List<Trade> trades)
    {
        if (trades.Count == 0)
            return new DashboardSummary(0, 0, 0, 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0d);

        var wins       = trades.Where(t => t.NetPnL > 0).ToList();
        var losses     = trades.Where(t => t.NetPnL < 0).ToList();
        var breakEvens = trades.Where(t => t.NetPnL == 0).ToList();

        var totalWins   = wins.Sum(t => t.NetPnL);
        var totalLosses = Math.Abs(losses.Sum(t => t.NetPnL));
        var profitFactor = totalLosses == 0 ? (totalWins > 0 ? 99.99m : 0m)
                                              : Math.Round(totalWins / totalLosses, 2);

        var winRate = trades.Count > 0 ? Math.Round((decimal)wins.Count / trades.Count * 100, 2) : 0m;

        var maxDd = ComputeMaxDrawdown(trades);

        var avgHoldMins = trades
            .Where(t => t.ExitTime.HasValue)
            .Select(t => (t.ExitTime!.Value - t.EntryTime).TotalMinutes)
            .DefaultIfEmpty(0)
            .Average();

        return new DashboardSummary(
            TotalTrades:            trades.Count,
            WinningTrades:          wins.Count,
            LosingTrades:           losses.Count,
            BreakEvenTrades:        breakEvens.Count,
            WinRate:                winRate,
            TotalNetPnL:            trades.Sum(t => t.NetPnL),
            TotalGrossPnL:          trades.Sum(t => t.GrossPnL),
            TotalCommission:        trades.Sum(t => t.Commission),
            TotalSwap:              trades.Sum(t => t.Swap),
            AverageWin:             wins.Count > 0 ? Math.Round(wins.Average(t => t.NetPnL), 2) : 0m,
            AverageLoss:            losses.Count > 0 ? Math.Round(losses.Average(t => t.NetPnL), 2) : 0m,
            ProfitFactor:           profitFactor,
            MaxDrawdown:            maxDd,
            LargestWin:             trades.Max(t => t.NetPnL),
            LargestLoss:            trades.Min(t => t.NetPnL),
            AverageHoldingTimeMinutes: Math.Round(avgHoldMins, 1)
        );
    }

    private static decimal ComputeMaxDrawdown(List<Trade> trades)
    {
        decimal peak       = 0m;
        decimal cumulative = 0m;
        decimal maxDd      = 0m;

        foreach (var t in trades)
        {
            cumulative += t.NetPnL;
            if (cumulative > peak) peak = cumulative;
            var dd = peak - cumulative;
            if (dd > maxDd) maxDd = dd;
        }
        return Math.Round(maxDd, 2);
    }

    private static List<MonthlyStats> ComputeMonthly(List<Trade> trades, int year)
    {
        return trades
            .GroupBy(t => t.ExitTime!.Value.Month)
            .Select(g =>
            {
                var wins   = g.Count(t => t.NetPnL > 0);
                var count  = g.Count();
                return new MonthlyStats(
                    Year:       year,
                    Month:      g.Key,
                    MonthName:  new DateTime(year, g.Key, 1).ToString("MMMM"),
                    TradeCount: count,
                    WinCount:   wins,
                    LossCount:  g.Count(t => t.NetPnL < 0),
                    NetPnL:     Math.Round(g.Sum(t => t.NetPnL), 2),
                    WinRate:    count > 0 ? Math.Round((decimal)wins / count * 100, 2) : 0m
                );
            })
            .OrderBy(m => m.Month)
            .ToList();
    }

    private static List<DailyStats> ComputeDaily(List<Trade> trades, int year, int? month)
    {
        return trades
            .GroupBy(t => t.ExitTime!.Value.Date)
            .Select(g =>
            {
                var wins  = g.Count(t => t.NetPnL > 0);
                var count = g.Count();
                return new DailyStats(
                    Date:       g.Key.ToString("yyyy-MM-dd"),
                    TradeCount: count,
                    WinCount:   wins,
                    LossCount:  g.Count(t => t.NetPnL < 0),
                    NetPnL:     Math.Round(g.Sum(t => t.NetPnL), 2)
                );
            })
            .OrderBy(d => d.Date)
            .ToList();
    }

    private static List<SymbolStats> ComputeSymbols(List<Trade> trades)
    {
        return trades
            .GroupBy(t => t.Symbol)
            .Select(g =>
            {
                var wins  = g.Count(t => t.NetPnL > 0);
                var count = g.Count();
                return new SymbolStats(
                    Symbol:     g.Key,
                    TradeCount: count,
                    WinCount:   wins,
                    LossCount:  g.Count(t => t.NetPnL < 0),
                    NetPnL:     Math.Round(g.Sum(t => t.NetPnL), 2),
                    WinRate:    count > 0 ? Math.Round((decimal)wins / count * 100, 2) : 0m
                );
            })
            .OrderByDescending(s => s.NetPnL)
            .ToList();
    }
}
