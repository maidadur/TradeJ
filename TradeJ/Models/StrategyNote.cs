namespace TradeJ.Models;

public class StrategyNote
{
    public int Id { get; set; }
    public int StrategyId { get; set; }
    public string Title { get; set; } = "Untitled";
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Strategy Strategy { get; set; } = null!;
}
