using Microsoft.AspNetCore.Mvc;
using TradeJ.Services;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(DashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int[] accountIds,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null)
    {
        var resolvedYear = year ?? DateTime.UtcNow.Year;
        var result = await dashboardService.GetDashboardAsync(accountIds, resolvedYear, month);
        return Ok(result);
    }
}
