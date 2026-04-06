namespace TradeJ.DTOs;

public record StrategyListItemDto(
    int Id,
    string Name,
    string? Description,
    string? ImageUrl,
    int TotalTrades,
    decimal NetPnL,
    decimal WinRate,
    decimal ProfitFactor,
    DateTime CreatedAt);

public record StrategyDetailDto(
    int Id,
    string Name,
    string? Description,
    string? ImageUrl,
    StrategyStatsDto Stats,
    List<StrategyTradeDto> Trades,
    List<StrategyNoteDto> Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record StrategyStatsDto(
    int TotalTrades,
    int Winners,
    int Losers,
    decimal NetPnL,
    decimal GrossPnL,
    decimal Commission,
    decimal WinRate,
    decimal ProfitFactor,
    decimal AvgTradeNetPnL,
    decimal AvgWin,
    decimal AvgLoss,
    double AvgHoldMinutes);

public record StrategyTradeDto(
    int Id,
    string Symbol,
    string Direction,
    string Status,
    DateTime EntryTime,
    DateTime? ExitTime,
    decimal EntryPrice,
    decimal? ExitPrice,
    decimal Volume,
    decimal NetPnL,
    decimal GrossPnL,
    int DurationMinutes);

public record StrategyNoteDto(
    int Id,
    string Title,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateStrategyDto(string Name, string? Description);
public record UpdateStrategyDto(string Name, string? Description);
public record UpdateStrategyStrategiesDto(List<int> StrategyIds);
public record CreateStrategyNoteDto(string Title, string Content);
public record UpdateStrategyNoteDto(string Title, string Content);
