namespace TradeJ.Models;

public class DayNote
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
