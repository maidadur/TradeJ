namespace TradeJ.Models;

public class DayTagDef
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6366f1";
    public ICollection<DayTag> DayTags { get; set; } = [];
}
