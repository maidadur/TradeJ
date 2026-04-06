namespace TradeJ.Models;

public class Strategy
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TradeStrategy> TradeStrategies { get; set; } = [];
    public ICollection<StrategyNote> Notes { get; set; } = [];
}
