namespace TradeJ.Models;

public class Tag
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public TagCategory Category { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public ICollection<TradeTag> TradeTags { get; set; } = [];
}
