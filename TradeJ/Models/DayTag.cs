namespace TradeJ.Models;

public class DayTag
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int DayTagDefId { get; set; }
    public DayTagDef DayTagDef { get; set; } = null!;
}
