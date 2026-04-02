namespace TradeJ.Models;

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Broker { get; set; } = string.Empty; // MT5 | cTrader | ByBit
    public string AccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Trade> Trades { get; set; } = [];
}
