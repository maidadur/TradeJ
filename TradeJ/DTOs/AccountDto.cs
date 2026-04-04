using System.Text.Json.Serialization;

namespace TradeJ.DTOs;

public record AccountDto(
    int Id,
    string Name,
    string Broker,
    string AccountNumber,
    string Currency,
    decimal InitialBalance,
    bool IsActive,
    DateTime CreatedAt,
    int TradeCount,
    [property: JsonPropertyName("mt5Server")]
    string? MT5Server,
    [property: JsonPropertyName("hasMT5InvestorPassword")]
    bool HasMT5InvestorPassword,
    string? MetaApiAccountId,
    bool HasMetaApiToken,
    string MetaApiRegion);

public record CreateAccountDto(
    string Name,
    string Broker,
    string AccountNumber,
    string Currency,
    decimal InitialBalance = 0m,
    string? MT5Server = null,
    string? MT5InvestorPassword = null,
    string? MetaApiAccountId = null,
    string? MetaApiToken = null,
    string MetaApiRegion = "london");

public record UpdateAccountDto(
    string Name,
    string AccountNumber,
    string Currency,
    decimal InitialBalance,
    bool IsActive,
    string? MT5Server = null,
    string? MT5InvestorPassword = null,
    string? MetaApiAccountId = null,
    string? MetaApiToken = null,
    string MetaApiRegion = "london");
