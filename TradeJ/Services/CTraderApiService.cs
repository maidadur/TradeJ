using System.Net.WebSockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Services;

/// <summary>
/// Communicates with the cTrader Open API using WebSocket + hand-rolled protobuf encoding.
/// No NuGet protobuf package required — only the net10 built-ins.
/// </summary>
public class CTraderApiService(IConfiguration config, AppDbContext db, IHttpClientFactory http)
{
    // WebSocket endpoints
    private const string LiveWs = "wss://live.ctraderapi.com:5035";
    private const string DemoWs = "wss://demo.ctraderapi.com:5035";

    // cTrader token endpoint
    private const string TokenEndpoint = "https://connect.spotware.com/apps/token";

    // ProtoOAPayloadType values used here
    private enum PT : uint
    {
        HeartbeatEvent   = 51,
        AppAuthReq       = 2100,
        AppAuthRes       = 2101,
        AccountAuthReq   = 2102,
        AccountAuthRes   = 2103,
        GetAccountsReq   = 2149,
        GetAccountsRes   = 2150,
        DealListReq      = 2161,
        DealListRes      = 2162,
        SymbolsListReq   = 2115,
        SymbolsListRes   = 2116,
        ErrorRes         = 2142,
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public string GetOAuthUrl()
    {
        var clientId    = RequireConfig("CTrader:ClientId");
        var redirectUri = RequireConfig("CTrader:RedirectUri");
        return $"https://connect.spotware.com/apps/authorize" +
               $"?response_type=code" +
               $"&client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&scope=trading";
    }

    /// <summary>Exchange auth code for an access token then list authorised accounts.</summary>
    public async Task<CTraderAccountsResponse> ExchangeAndListAccountsAsync(string code)
    {
        var clientId     = RequireConfig("CTrader:ClientId");
        var clientSecret = RequireConfig("CTrader:ClientSecret");
        var redirectUri  = RequireConfig("CTrader:RedirectUri");

        // 1. Exchange code → access token + refresh token
        using var client = http.CreateClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["redirect_uri"]  = redirectUri,
            ["client_id"]     = clientId,
            ["client_secret"] = clientSecret,
        });

        var tokenRes = await client.PostAsync(TokenEndpoint, form);
        if (!tokenRes.IsSuccessStatusCode)
        {
            var body = await tokenRes.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Token exchange failed ({tokenRes.StatusCode}): {body}");
        }

        var tokenJson    = await tokenRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var accessToken  = tokenJson.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("access_token missing from response");
        var refreshToken = tokenJson.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";

        // 2. List accounts via cTrader Open API
        using var ws = await ConnectAndAppAuthAsync();
        var payload = Encode(ms => WriteStr(ms, 1, accessToken));
        await SendAsync(ws, (uint)PT.GetAccountsReq, payload);
        var res = await ReceiveUntilAsync(ws, (uint)PT.GetAccountsRes);

        var accounts = ParseAccounts(res);
        return new CTraderAccountsResponse(accessToken, refreshToken, accounts);
    }

    /// <summary>Use a stored refresh token to obtain a fresh access token.</summary>
    public async Task<string> RefreshAccessTokenAsync(string refreshToken)
    {
        var clientId     = RequireConfig("CTrader:ClientId");
        var clientSecret = RequireConfig("CTrader:ClientSecret");

        using var client = http.CreateClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"]     = clientId,
            ["client_secret"] = clientSecret,
        });

        var tokenRes = await client.PostAsync(TokenEndpoint, form);
        if (!tokenRes.IsSuccessStatusCode)
        {
            var body = await tokenRes.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Token refresh failed ({tokenRes.StatusCode}): {body}");
        }

        var tokenJson   = await tokenRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return tokenJson.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("access_token missing from refresh response");
    }

    /// <summary>Persist cTrader link credentials on the TradeJ account for auto-sync.</summary>
    public async Task LinkAccountAsync(CTraderLinkRequest req)
    {
        var account = await db.Accounts.FindAsync(req.TradeJAccountId)
            ?? throw new InvalidOperationException($"Account {req.TradeJAccountId} not found.");

        account.CTraderCtidAccountId = req.CtidTraderAccountId;
        account.CTraderIsLive        = req.IsLive;
        account.CTraderRefreshToken  = req.RefreshToken;
        await db.SaveChangesAsync();
    }

    /// <summary>Import closed positions for one cTrader account into a TradeJ account.</summary>
    public async Task<ImportResultDto> ImportAsync(CTraderImportRequest req)
    {
        var account = await db.Accounts.FindAsync(req.TradeJAccountId)
            ?? throw new InvalidOperationException($"TradeJ account {req.TradeJAccountId} not found.");

        using var ws = await ConnectAndAppAuthAsync(req.IsLive);
        await AccountAuthAsync(ws, req.CtidTraderAccountId, req.AccessToken);

        var symbolMap = await GetSymbolMapAsync(ws, req.CtidTraderAccountId);

        // Extend from-date backwards to capture opening deals for positions
        var extendedFrom = req.DateFrom.AddYears(-1);
        var allDeals     = await FetchDealsAsync(ws, req.CtidTraderAccountId, extendedFrom, req.DateTo);

        var requestedFromMs = new DateTimeOffset(req.DateFrom.ToUniversalTime()).ToUnixTimeMilliseconds();
        var requestedToMs   = new DateTimeOffset(req.DateTo.ToUniversalTime()).ToUnixTimeMilliseconds();

        var existing = await db.Trades
            .Where(t => t.AccountId == req.TradeJAccountId)
            .Select(t => t.BrokerTradeId)
            .ToHashSetAsync();

        var newTrades = new List<Trade>();
        var skipped   = 0;

        // Group all fetched deals by positionId
        var byPosition = allDeals
            .GroupBy(d => d.PositionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (posId, posDealList) in byPosition)
        {
            // Only process positions whose close falls within the user-requested range
            var closingDeals = posDealList
                .Where(d => d.IsClosingDeal
                         && d.ExecutionTimestamp >= requestedFromMs
                         && d.ExecutionTimestamp <= requestedToMs)
                .OrderBy(d => d.ExecutionTimestamp)
                .ToList();

            if (closingDeals.Count == 0) continue;

            var brokerTradeId = $"ct_{posId}";
            if (existing.Contains(brokerTradeId)) { skipped++; continue; }

            var lastClose  = closingDeals[^1];
            var direction  = lastClose.TradeSide == 2   // SELL closes a LONG
                ? TradeDirection.Long
                : TradeDirection.Short;

            // Best open time: earliest non-closing deal; fallback to close createTimestamp
            var openingDeal = posDealList
                .Where(d => !d.IsClosingDeal)
                .OrderBy(d => d.ExecutionTimestamp)
                .FirstOrDefault();

            var entryTime = openingDeal is not null
                ? DateTimeOffset.FromUnixTimeMilliseconds(openingDeal.ExecutionTimestamp).UtcDateTime
                : DateTimeOffset.FromUnixTimeMilliseconds(lastClose.CreateTimestamp).UtcDateTime;

            var exitTime = DateTimeOffset.FromUnixTimeMilliseconds(lastClose.ExecutionTimestamp).UtcDateTime;

            // Entry price from closePositionDetail (same for all partial closes of this position)
            var entryPrice = (decimal)lastClose.EntryPrice;

            // Volume-weighted exit price across all closing deals
            long   totalClosedVol = closingDeals.Sum(d => d.ClosedVolume);
            decimal exitPrice = totalClosedVol > 0
                ? (decimal)(closingDeals.Sum(d => d.ExecutionPrice * d.ClosedVolume) / totalClosedVol)
                : (decimal)lastClose.ExecutionPrice;

            // Financials (sum across all partial closes)
            var moneyDigits = lastClose.MoneyDigits > 0 ? lastClose.MoneyDigits : 2;
            var divisor     = (decimal)Math.Pow(10, moneyDigits);

            var grossPnL   = closingDeals.Sum(d => d.CloseProfit)      / divisor;
            var commission = closingDeals.Sum(d => d.CloseCommission)   / divisor;
            var swap       = closingDeals.Sum(d => d.CloseSwap)         / divisor;
            var netPnL     = grossPnL + commission + swap;

            var symbolId = posDealList[0].SymbolId;
            var symbol   = symbolMap.TryGetValue(symbolId, out var sym) ? sym : $"SYM_{symbolId}";
            var volume   = totalClosedVol / 100m;

            newTrades.Add(new Trade
            {
                AccountId     = req.TradeJAccountId,
                BrokerTradeId = brokerTradeId,
                Symbol        = symbol,
                Direction     = direction,
                Status        = TradeStatus.Closed,
                EntryPrice    = entryPrice,
                ExitPrice     = exitPrice,
                EntryTime     = entryTime,
                ExitTime      = exitTime,
                Volume        = volume,
                GrossPnL      = grossPnL,
                Commission    = commission,
                Swap          = swap,
                NetPnL        = netPnL,
                ImportedAt    = DateTime.UtcNow,
            });
            existing.Add(brokerTradeId);
        }

        if (newTrades.Count > 0)
        {
            db.Trades.AddRange(newTrades);
            await db.SaveChangesAsync();
        }

        return new ImportResultDto(newTrades.Count, skipped, 0, []);
    }

    // ── WebSocket connection helpers ──────────────────────────────────────────

    private async Task<ClientWebSocket> ConnectAndAppAuthAsync(bool isLive = true)
    {
        var ws  = new ClientWebSocket();
        var url = isLive ? LiveWs : DemoWs;
        await ws.ConnectAsync(new Uri(url), CancellationToken.None);

        var clientId     = RequireConfig("CTrader:ClientId");
        var clientSecret = RequireConfig("CTrader:ClientSecret");

        var payload = Encode(ms => { WriteStr(ms, 1, clientId); WriteStr(ms, 2, clientSecret); });
        await SendAsync(ws, (uint)PT.AppAuthReq, payload);
        await ReceiveUntilAsync(ws, (uint)PT.AppAuthRes);

        return ws;
    }

    private static async Task AccountAuthAsync(ClientWebSocket ws, long accountId, string accessToken)
    {
        var payload = Encode(ms => { WriteI64(ms, 1, accountId); WriteStr(ms, 2, accessToken); });
        await SendAsync(ws, (uint)PT.AccountAuthReq, payload);
        await ReceiveUntilAsync(ws, (uint)PT.AccountAuthRes);
    }

    private static async Task<Dictionary<long, string>> GetSymbolMapAsync(ClientWebSocket ws, long accountId)
    {
        var payload = Encode(ms => WriteI64(ms, 1, accountId));
        await SendAsync(ws, (uint)PT.SymbolsListReq, payload);
        var res = await ReceiveUntilAsync(ws, (uint)PT.SymbolsListRes, 30_000);
        return ParseSymbolMap(res);
    }

    private static async Task<List<RawDeal>> FetchDealsAsync(
        ClientWebSocket ws, long accountId, DateTime from, DateTime to)
    {
        var fromMs = new DateTimeOffset(from.ToUniversalTime()).ToUnixTimeMilliseconds();
        var toMs   = new DateTimeOffset(to.ToUniversalTime()).ToUnixTimeMilliseconds();
        var all    = new List<RawDeal>();
        const int PageSize = 5000;
        long cursor = fromMs;

        while (cursor <= toMs)
        {
            var payload = Encode(ms =>
            {
                WriteI64(ms, 1, accountId);
                WriteI64(ms, 2, cursor);
                WriteI64(ms, 3, toMs);
                WriteI32(ms, 4, PageSize);
            });
            await SendAsync(ws, (uint)PT.DealListReq, payload);
            var res = await ReceiveUntilAsync(ws, (uint)PT.DealListRes, 30_000);

            var (batch, hasMore) = ParseDeals(res);
            all.AddRange(batch);

            if (!hasMore || batch.Count == 0) break;

            var lastTs = batch.Max(d => d.ExecutionTimestamp);
            if (lastTs <= cursor) break;
            cursor = lastTs + 1;
        }

        return all;
    }

    // ── WebSocket I/O ─────────────────────────────────────────────────────────

    private static async Task SendAsync(ClientWebSocket ws, uint payloadType, byte[] payload)
    {
        var msg = Encode(ms => { WriteU32(ms, 1, payloadType); WriteBytes(ms, 2, payload); });
        await ws.SendAsync(new ArraySegment<byte>(msg), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    private static async Task<byte[]> ReceiveUntilAsync(
        ClientWebSocket ws, uint expectedType, int timeoutMs = 15_000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var buf = new byte[4 * 1024 * 1024]; // 4 MB receive buffer

        while (true)
        {
            var r    = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
            var data = buf.AsSpan(0, r.Count).ToArray();

            var (type, inner, _) = DecodeEnvelope(data);

            if (type == (uint)PT.ErrorRes)
                throw new InvalidOperationException($"cTrader API error: {ParseErrorMsg(inner)}");
            if (type == (uint)PT.HeartbeatEvent) continue;
            if (type == expectedType) return inner;
        }
    }

    // ── Protobuf encoding ─────────────────────────────────────────────────────

    private static byte[] Encode(Action<MemoryStream> write)
    {
        using var ms = new MemoryStream();
        write(ms);
        return ms.ToArray();
    }

    private static void WriteStr(Stream s, int field, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteTag(s, field, 2); WriteVarInt(s, (ulong)bytes.Length); s.Write(bytes);
    }

    private static void WriteBytes(Stream s, int field, byte[] value)
    { WriteTag(s, field, 2); WriteVarInt(s, (ulong)value.Length); s.Write(value); }

    private static void WriteU32(Stream s, int field, uint value)
    { WriteTag(s, field, 0); WriteVarInt(s, value); }

    private static void WriteI32(Stream s, int field, int value)
    { WriteTag(s, field, 0); WriteVarInt(s, (ulong)(long)value); }

    private static void WriteI64(Stream s, int field, long value)
    { WriteTag(s, field, 0); WriteVarInt(s, (ulong)value); }

    private static void WriteTag(Stream s, int field, int wireType)
        => WriteVarInt(s, (ulong)(field << 3 | wireType));

    private static void WriteVarInt(Stream s, ulong v)
    { while (v >= 0x80) { s.WriteByte((byte)((v & 0x7F) | 0x80)); v >>= 7; } s.WriteByte((byte)v); }

    // ── Protobuf decoding ─────────────────────────────────────────────────────

    private static (uint type, byte[] payload, string? msgId) DecodeEnvelope(byte[] data)
    {
        uint type = 0; byte[]? payload = null; string? msgId = null;
        int pos = 0;
        while (pos < data.Length)
        {
            var tag   = ReadVarU32(data, ref pos);
            var field = tag >> 3;
            var wire  = tag & 7;
            switch (field)
            {
                case 1: type    = ReadVarU32(data, ref pos); break;
                case 2: payload = ReadLD(data, ref pos); break;
                case 3: msgId   = Encoding.UTF8.GetString(ReadLD(data, ref pos)); break;
                default: Skip(data, ref pos, wire); break;
            }
        }
        return (type, payload ?? [], msgId);
    }

    private static List<CTraderAccountDto> ParseAccounts(byte[] data)
    {
        var result = new List<CTraderAccountDto>();
        int pos = 0;
        while (pos < data.Length)
        {
            var tag = ReadVarU32(data, ref pos);
            if ((tag >> 3) == 2) result.Add(ParseAccount(ReadLD(data, ref pos)));
            else Skip(data, ref pos, tag & 7);
        }
        return result;
    }

    private static CTraderAccountDto ParseAccount(byte[] data)
    {
        long id = 0; bool isLive = false; long login = 0; string broker = "";
        int pos = 0;
        while (pos < data.Length)
        {
            var tag = ReadVarU32(data, ref pos);
            switch (tag >> 3)
            {
                case 1: id     = (long)ReadVarU64(data, ref pos); break;
                case 2: isLive = ReadVarU32(data, ref pos) != 0; break;
                case 3: login  = (long)ReadVarU64(data, ref pos); break;
                case 9: broker = Encoding.UTF8.GetString(ReadLD(data, ref pos)); break;
                default: Skip(data, ref pos, tag & 7); break;
            }
        }
        return new CTraderAccountDto(id, isLive, login, broker);
    }

    private static Dictionary<long, string> ParseSymbolMap(byte[] data)
    {
        var map = new Dictionary<long, string>();
        int pos = 0;
        while (pos < data.Length)
        {
            var tag  = ReadVarU32(data, ref pos);
            var wire = tag & 7;
            if ((tag >> 3) == 2 && wire == 2)
            {
                var (id, name) = ParseLightSymbol(ReadLD(data, ref pos));
                if (id > 0 && !string.IsNullOrEmpty(name)) map[id] = name;
            }
            else Skip(data, ref pos, wire);
        }
        return map;
    }

    private static (long id, string name) ParseLightSymbol(byte[] data)
    {
        long id = 0; string name = "";
        int pos = 0;
        while (pos < data.Length)
        {
            var tag = ReadVarU32(data, ref pos);
            switch (tag >> 3)
            {
                case 1: id   = (long)ReadVarU64(data, ref pos); break;
                case 2: name = Encoding.UTF8.GetString(ReadLD(data, ref pos)); break;
                default: Skip(data, ref pos, tag & 7); break;
            }
        }
        return (id, name);
    }

    private static (List<RawDeal> deals, bool hasMore) ParseDeals(byte[] data)
    {
        var deals   = new List<RawDeal>();
        bool hasMore = false;
        int pos = 0;
        while (pos < data.Length)
        {
            var tag  = ReadVarU32(data, ref pos);
            var wire = tag & 7;
            if ((tag >> 3) == 2 && wire == 2)
                deals.Add(ParseDeal(ReadLD(data, ref pos)));
            else if ((tag >> 3) == 3 && wire == 0)
                hasMore = ReadVarU32(data, ref pos) != 0;
            else
                Skip(data, ref pos, wire);
        }
        return (deals, hasMore);
    }

    private static RawDeal ParseDeal(byte[] data)
    {
        var d = new RawDeal();
        int pos = 0;
        while (pos < data.Length)
        {
            var tag  = ReadVarU32(data, ref pos);
            var wire = tag & 7;
            switch (tag >> 3)
            {
                case 1:  d.DealId             = (long)ReadVarU64(data, ref pos); break;
                case 3:  d.PositionId         = (long)ReadVarU64(data, ref pos); break;
                case 4:  d.SymbolId           = (long)ReadVarU64(data, ref pos); break;
                case 5:  d.CreateTimestamp    = (long)ReadVarU64(data, ref pos); break;
                case 6:  d.ExecutionTimestamp = (long)ReadVarU64(data, ref pos); break;
                case 7:  d.TradeSide          = (int)ReadVarU32(data, ref pos); break;
                case 8:  d.FilledVolume       = (long)ReadVarU64(data, ref pos); break;
                case 9:  d.ExecutionPrice     = ReadDouble(data, ref pos); break;
                case 13: d.DealStatus         = (int)ReadVarU32(data, ref pos); break;
                case 15: d.CloseDetailBytes   = ReadLD(data, ref pos); ParseCloseDetail(d); break;
                default: Skip(data, ref pos, wire); break;
            }
        }
        return d;
    }

    private static void ParseCloseDetail(RawDeal d)
    {
        var data = d.CloseDetailBytes!;
        int pos  = 0;
        while (pos < data.Length)
        {
            var tag  = ReadVarU32(data, ref pos);
            var wire = tag & 7;
            switch (tag >> 3)
            {
                case 1:  d.EntryPrice      = ReadDouble(data, ref pos); break;
                case 2:  d.CloseProfit     = (long)ReadVarU64(data, ref pos); break;
                case 3:  d.CloseSwap       = (long)ReadVarU64(data, ref pos); break;
                case 4:  d.CloseCommission = (long)ReadVarU64(data, ref pos); break;
                case 7:  d.ClosedVolume    = (long)ReadVarU64(data, ref pos); break;
                case 10: d.MoneyDigits     = (int)ReadVarU32(data, ref pos); break;
                default: Skip(data, ref pos, wire); break;
            }
        }
    }

    private static string ParseErrorMsg(byte[] data)
    {
        int pos = 0;
        while (pos < data.Length)
        {
            var tag = ReadVarU32(data, ref pos);
            if ((tag >> 3) == 2) return Encoding.UTF8.GetString(ReadLD(data, ref pos));
            Skip(data, ref pos, tag & 7);
        }
        return "Unknown cTrader API error";
    }

    // ── Low-level protobuf read helpers ───────────────────────────────────────

    private static uint ReadVarU32(byte[] d, ref int pos)
    {
        uint v = 0; int shift = 0;
        while (pos < d.Length) { var b = d[pos++]; v |= (uint)(b & 0x7F) << shift; if ((b & 0x80) == 0) break; shift += 7; }
        return v;
    }

    private static ulong ReadVarU64(byte[] d, ref int pos)
    {
        ulong v = 0; int shift = 0;
        while (pos < d.Length) { var b = d[pos++]; v |= (ulong)(b & 0x7F) << shift; if ((b & 0x80) == 0) break; shift += 7; }
        return v;
    }

    private static byte[] ReadLD(byte[] d, ref int pos)
    {
        var len = (int)ReadVarU32(d, ref pos);
        var bytes = d[pos..(pos + len)]; pos += len; return bytes;
    }

    private static double ReadDouble(byte[] d, ref int pos)
    {
        // Wire type 1 = 64-bit little-endian
        var span = d.AsSpan(pos, 8); pos += 8;
        return BitConverter.IsLittleEndian
            ? BitConverter.ToDouble(span)
            : BitConverter.ToDouble([span[7], span[6], span[5], span[4], span[3], span[2], span[1], span[0]]);
    }

    private static void Skip(byte[] d, ref int pos, uint wireType)
    {
        switch (wireType)
        {
            case 0: ReadVarU64(d, ref pos); break;
            case 1: pos += 8; break;
            case 2: pos += (int)ReadVarU32(d, ref pos); break;
            case 5: pos += 4; break;
        }
    }

    private string RequireConfig(string key)
        => config[key] ?? throw new InvalidOperationException(
            $"'{key}' is not configured. Add it to appsettings.json under CTrader section.");
}

/// <summary>Internal model holding raw data from one cTrader API deal message.</summary>
internal sealed class RawDeal
{
    public long   DealId             { get; set; }
    public long   PositionId         { get; set; }
    public long   SymbolId           { get; set; }
    public long   CreateTimestamp    { get; set; }
    public long   ExecutionTimestamp { get; set; }
    public int    TradeSide          { get; set; }   // 1=BUY, 2=SELL
    public long   FilledVolume       { get; set; }   // in 1/100 lots
    public double ExecutionPrice     { get; set; }
    public int    DealStatus         { get; set; }

    // Set when this deal closes (or partially closes) a position
    public byte[]? CloseDetailBytes  { get; set; }
    public double  EntryPrice        { get; set; }
    public long    CloseProfit       { get; set; }
    public long    CloseSwap         { get; set; }
    public long    CloseCommission   { get; set; }
    public long    ClosedVolume      { get; set; }   // in 1/100 lots
    public int     MoneyDigits       { get; set; }

    public bool IsClosingDeal => CloseDetailBytes is not null;
}
