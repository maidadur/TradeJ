using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChecklistItemsController(AppDbContext db) : ControllerBase
{
    // POST /api/checklistitems?strategyId=1
    [HttpPost]
    public async Task<ActionResult<ChecklistItemDto>> Create(
        [FromQuery] int strategyId,
        [FromBody] CreateChecklistItemDto dto)
    {
        var strategy = await db.Strategies.FindAsync(strategyId);
        if (strategy is null) return NotFound();

        var maxOrder = await db.ChecklistItems
            .Where(c => c.StrategyId == strategyId)
            .Select(c => (int?)c.OrderIndex)
            .MaxAsync() ?? -1;

        var item = new ChecklistItem
        {
            StrategyId = strategyId,
            Text       = dto.Text,
            OrderIndex = maxOrder + 1,
            IsChecked  = false
        };
        db.ChecklistItems.Add(item);
        await db.SaveChangesAsync();

        return Ok(ToDto(item));
    }

    // PUT /api/checklistitems/{id}
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ChecklistItemDto>> Update(int id, [FromBody] UpdateChecklistItemDto dto)
    {
        var item = await db.ChecklistItems.FindAsync(id);
        if (item is null) return NotFound();

        item.Text       = dto.Text;
        item.OrderIndex = dto.OrderIndex;
        item.IsChecked  = dto.IsChecked;
        await db.SaveChangesAsync();

        return Ok(ToDto(item));
    }

    // DELETE /api/checklistitems/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await db.ChecklistItems.FindAsync(id);
        if (item is null) return NotFound();

        db.ChecklistItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/checklistitems/reorder?strategyId=1
    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder([FromQuery] int strategyId, [FromBody] ReorderChecklistItemsDto dto)
    {
        var items = await db.ChecklistItems
            .Where(c => c.StrategyId == strategyId)
            .ToListAsync();

        for (var i = 0; i < dto.OrderedIds.Count; i++)
        {
            var item = items.FirstOrDefault(c => c.Id == dto.OrderedIds[i]);
            if (item is not null) item.OrderIndex = i;
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/checklistitems/reset?strategyId=1
    [HttpPost("reset")]
    public async Task<IActionResult> Reset([FromQuery] int strategyId)
    {
        var items = await db.ChecklistItems
            .Where(c => c.StrategyId == strategyId)
            .ToListAsync();

        foreach (var item in items) item.IsChecked = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static ChecklistItemDto ToDto(ChecklistItem c) =>
        new(c.Id, c.Text, c.OrderIndex, c.IsChecked);
}
