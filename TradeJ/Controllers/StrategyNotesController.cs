using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StrategyNotesController(AppDbContext db) : ControllerBase
{
    // POST /api/strategynotes?strategyId=1
    [HttpPost]
    public async Task<ActionResult<StrategyNoteDto>> Create(
        [FromQuery] int strategyId,
        [FromBody] CreateStrategyNoteDto dto)
    {
        var strategy = await db.Strategies.FindAsync(strategyId);
        if (strategy is null) return NotFound();

        var note = new StrategyNote
        {
            StrategyId = strategyId,
            Title      = dto.Title,
            Content    = dto.Content,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow
        };
        db.StrategyNotes.Add(note);
        await db.SaveChangesAsync();

        return Ok(ToDto(note));
    }

    // PUT /api/strategynotes/{id}
    [HttpPut("{id:int}")]
    public async Task<ActionResult<StrategyNoteDto>> Update(int id, [FromBody] UpdateStrategyNoteDto dto)
    {
        var note = await db.StrategyNotes.FindAsync(id);
        if (note is null) return NotFound();

        note.Title     = dto.Title;
        note.Content   = dto.Content;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ToDto(note));
    }

    // DELETE /api/strategynotes/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await db.StrategyNotes.FindAsync(id);
        if (note is null) return NotFound();

        db.StrategyNotes.Remove(note);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static StrategyNoteDto ToDto(StrategyNote n) =>
        new(n.Id, n.Title, n.Content, n.CreatedAt, n.UpdatedAt);
}
