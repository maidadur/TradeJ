namespace TradeJ.Models;

public class TradeStrategy
{
    public int TradeId { get; set; }
    public int StrategyId { get; set; }

    public Trade Trade { get; set; } = null!;
    public Strategy Strategy { get; set; } = null!;
}
