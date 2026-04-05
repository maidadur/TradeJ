namespace TradeJ.Models;

/// <summary>
/// Simple key-value store for app-wide settings persisted in the database.
/// </summary>
public class AppSetting
{
    public string Key   { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
