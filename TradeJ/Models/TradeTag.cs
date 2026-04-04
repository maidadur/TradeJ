namespace TradeJ.Models;

public class TradeTag
{
    public int Id { get; set; }
    public int TradeId { get; set; }
    public Trade Trade { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
