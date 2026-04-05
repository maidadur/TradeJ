namespace TradeJ.DTOs;

public record CTraderOAuthUrlResponse(string Url);

public record CTraderExchangeRequest(string Code);

public record CTraderAccountDto(
    long CtidTraderAccountId,
    bool IsLive,
    long TraderLogin,
    string BrokerName);

public record CTraderAccountsResponse(
    string AccessToken,
    string RefreshToken,
    List<CTraderAccountDto> Accounts);

public record CTraderImportRequest(
    string AccessToken,
    long CtidTraderAccountId,
    bool IsLive,
    int TradeJAccountId,
    DateTime DateFrom,
    DateTime DateTo);

/// <summary>
/// Sent by the frontend after a successful import to persist the cTrader
/// link on the TradeJ account so auto-sync can run without another login.
/// </summary>
public record CTraderLinkRequest(
    int TradeJAccountId,
    long CtidTraderAccountId,
    bool IsLive,
    string RefreshToken);
