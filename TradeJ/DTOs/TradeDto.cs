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
    decimal? StopLoss,
    decimal? TakeProfit,
    decimal Volume,
    decimal GrossPnL,
    decimal Commission,
    decimal Swap,
    decimal NetPnL,
    decimal? RR,
    decimal? ActualRR,
    decimal? RiskPercent,
    string? Notes,
    string? Tags,
    bool IsRevoked,
    DateTime ImportedAt,
    List<int> TagIds,
    List<int> StrategyIds,
    int? MergedIntoTradeId,
    List<int> MergedTradeIds);

public record UpdateTradeMetricsDto(
    decimal? RR,
    decimal? ActualRR,
    decimal? RiskPercent);

public record UpdateTradeNotesDto(
    string? Notes,
    string? Tags);

public record UpdateTradeTagsDto(List<int> TagIds);

public record UpdateTradeStrategiesDto(List<int> StrategyIds);

public record UpdateTradeRevokedDto(bool IsRevoked);

public record MergeTradesDto(List<int> TradeIds);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
