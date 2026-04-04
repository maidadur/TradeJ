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

    public decimal Volume { get; set; }

    public decimal GrossPnL { get; set; }
    public decimal Commission { get; set; }
    public decimal Swap { get; set; }
    public decimal NetPnL { get; set; }

    public string? Notes { get; set; }
    public string? Tags { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TradeTag> TradeTags { get; set; } = [];
    public ICollection<TradeStrategy> TradeStrategies { get; set; } = [];
}
