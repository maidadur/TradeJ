namespace TradeJ.Models;

public class TagCategory
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6366f1";
    public int SortOrder { get; set; }
    public ICollection<Tag> Tags { get; set; } = [];
}
