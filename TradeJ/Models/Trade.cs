namespace TradeJ.Models;

public enum TradeDirection { Long, Short }
public enum TradeStatus { Open, Closed, Cancelled }

public class Trade
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public string BrokerTradeId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public TradeDirection Direction { get; set; }
    public TradeStatus Status { get; set; }

    public decimal EntryPrice { get; set; }
    public decimal? ExitPrice { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }

    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }

    public decimal Volume { get; set; }

    public decimal GrossPnL { get; set; }
    public decimal Commission { get; set; }
    public decimal Swap { get; set; }
    public decimal NetPnL { get; set; }

    public decimal? RR { get; set; }
    public decimal? ActualRR { get; set; }
    public decimal? RiskPercent { get; set; }

    public string? Notes { get; set; }
    public string? Tags { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when this trade was folded into a merged trade; null otherwise.</summary>
    public int? MergedIntoTradeId { get; set; }
    public Trade? MergedInto { get; set; }

    /// <summary>Source trades folded into this one, when this trade is a merge result.</summary>
    public ICollection<Trade> MergedTrades { get; set; } = [];

    public ICollection<TradeTag> TradeTags { get; set; } = [];
    public ICollection<TradeStrategy> TradeStrategies { get; set; } = [];
}
