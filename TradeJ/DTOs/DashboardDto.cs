namespace TradeJ.DTOs;

public record DashboardSummary(
    int TotalTrades,
    int WinningTrades,
    int LosingTrades,
    int BreakEvenTrades,
    decimal WinRate,
    decimal TotalNetPnL,
    decimal TotalGrossPnL,
    decimal TotalCommission,
    decimal TotalSwap,
    decimal AverageWin,
    decimal AverageLoss,
    decimal ProfitFactor,
    decimal MaxDrawdown,
    decimal LargestWin,
    decimal LargestLoss,
    double AverageHoldingTimeMinutes);

public record MonthlyStats(
    int Year,
    int Month,
    string MonthName,
    int TradeCount,
    int WinCount,
    int LossCount,
    decimal NetPnL,
    decimal WinRate);

public record DailyStats(
    string Date,
    int TradeCount,
    int WinCount,
    int LossCount,
    decimal NetPnL);

public record SymbolStats(
    string Symbol,
    int TradeCount,
    int WinCount,
    int LossCount,
    decimal NetPnL,
    decimal WinRate);

public record DashboardDto(
    DashboardSummary Summary,
    List<MonthlyStats> MonthlyStats,
    List<DailyStats> DailyStats,
    List<SymbolStats> SymbolStats);
