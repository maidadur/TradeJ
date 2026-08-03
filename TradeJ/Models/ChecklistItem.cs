namespace TradeJ.Models;

public class ChecklistItem
{
    public int Id { get; set; }
    public int StrategyId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public bool IsChecked { get; set; }

    public Strategy Strategy { get; set; } = null!;
}
