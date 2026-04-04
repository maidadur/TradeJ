namespace TradeJ.DTOs;

public record TradeDto(
    int Id,
    int AccountId,
    string AccountName,
    string BrokerTradeId,
    string Symbol,
    string Direction,
    string Status,
    decimal EntryPrice,
    decimal? ExitPrice,
    DateTime EntryTime,
    DateTime? ExitTime,
    decimal Volume,
    decimal GrossPnL,
    decimal Commission,
    decimal Swap,
    decimal NetPnL,
    string? Notes,
    string? Tags,
    DateTime ImportedAt,
    List<int> TagIds,
    List<int> StrategyIds);

public record UpdateTradeNotesDto(
    string? Notes,
    string? Tags);

public record UpdateTradeTagsDto(List<int> TagIds);

public record UpdateTradeStrategiesDto(List<int> StrategyIds);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
