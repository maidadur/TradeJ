namespace TradeJ.DTOs;

public record DayViewDto(List<DayGroup> Days);

public record DayGroup(
    string Date,
    string DayLabel,
    int WeekNumber,
    DayStats Stats,
    List<DayTradeItem> Trades,
    string? Note);

public record DayNoteDto(int Id, string Date, string Content, DateTime UpdatedAt);

public record SaveDayNoteRequest(string Content);

public record DayStats(
    int TotalTrades,
    int Winners,
    int Losers,
    decimal GrossPnL,
    decimal NetPnL,
    decimal Commission,
    decimal Swap,
    decimal Volume,
    decimal WinRate,
    decimal ProfitFactor);

public record DayTradeItem(
    int Id,
    string Symbol,
    string Direction,
    string Status,
    DateTime EntryTime,
    DateTime? ExitTime,
    decimal EntryPrice,
    decimal? ExitPrice,
    decimal Volume,
    decimal GrossPnL,
    decimal Commission,
    decimal Swap,
    decimal NetPnL,
    string? Tags,
    int DurationMinutes);
