using Microsoft.AspNetCore.Mvc;
using TradeJ.Services;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(AppSettingsService settings) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyDictionary<string, string>> GetAll()
        => Ok(settings.GetAll());

    [HttpPatch("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] string value)
    {
        // Only allow known keys to prevent arbitrary writes
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppSettingsService.KeyMt5SyncEnabled,
            AppSettingsService.KeyCTraderSyncEnabled,
        };

        if (!allowed.Contains(key))
            return BadRequest(new { message = $"Unknown setting key '{key}'." });

        await settings.SetAsync(key, value);
        return Ok();
    }
}
