using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.Models;

namespace TradeJ.Services;

/// <summary>
/// Singleton service that caches app settings from the database and keeps them
/// in sync. Background services read the flags without hitting the DB every tick.
/// </summary>
public sealed class AppSettingsService(IServiceScopeFactory scopeFactory, ILogger<AppSettingsService> logger)
{
    // Well-known keys
    public const string KeyMt5SyncEnabled     = "AutoSync.Mt5Enabled";
    public const string KeyCTraderSyncEnabled  = "AutoSync.CTraderEnabled";

    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);

    // ── Defaults (applied when the key does not exist in the DB) ─────────────
    private static readonly Dictionary<string, string> Defaults = new()
    {
        [KeyMt5SyncEnabled]    = "true",
        [KeyCTraderSyncEnabled] = "true",
    };

    /// <summary>Load all settings from DB into the cache. Called once at startup.</summary>
    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var stored = await db.AppSettings.ToListAsync();
            foreach (var s in stored)
                _cache[s.Key] = s.Value;

            // Seed any missing defaults
            bool changed = false;
            foreach (var (key, defaultValue) in Defaults)
            {
                if (!_cache.ContainsKey(key))
                {
                    _cache[key] = defaultValue;
                    db.AppSettings.Add(new AppSetting { Key = key, Value = defaultValue });
                    changed = true;
                }
            }
            if (changed) await db.SaveChangesAsync();
        }
        finally { _lock.Release(); }
    }

    public bool Get(string key)
    {
        if (_cache.TryGetValue(key, out var v))
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        if (Defaults.TryGetValue(key, out var d))
            return string.Equals(d, "true", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public bool Mt5SyncEnabled     => Get(KeyMt5SyncEnabled);
    public bool CTraderSyncEnabled => Get(KeyCTraderSyncEnabled);

    /// <summary>Update a setting in both the cache and the database.</summary>
    public async Task SetAsync(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            _cache[key] = value;

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existing = await db.AppSettings.FindAsync(key);
            if (existing is null)
                db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            else
                existing.Value = value;

            await db.SaveChangesAsync();
        }
        finally { _lock.Release(); }
    }

    /// <summary>Returns all settings as a dictionary.</summary>
    public IReadOnlyDictionary<string, string> GetAll()
    {
        lock (_cache) return new Dictionary<string, string>(_cache, StringComparer.OrdinalIgnoreCase);
    }
}
