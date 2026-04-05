namespace TradeJ.Models;

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Broker { get; set; } = string.Empty; // MT5 | cTrader | ByBit
    public string AccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal InitialBalance { get; set; } = 0m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // MT5 direct sync (login = AccountNumber; investor password is read-only in MT5 by design)
    public string? MT5Server { get; set; }
    public string? MT5InvestorPassword { get; set; }

    // MetaApi live import credentials
    public string? MetaApiAccountId { get; set; }
    public string? MetaApiToken { get; set; }
    public string MetaApiRegion { get; set; } = "london";

    // cTrader auto-sync — stored after first OAuth login
    public long? CTraderCtidAccountId { get; set; }   // ctidTraderAccountId from cTrader API
    public bool CTraderIsLive { get; set; } = true;
    public string? CTraderRefreshToken { get; set; }  // used to get a new access token silently

    public ICollection<Trade> Trades { get; set; } = [];
}
