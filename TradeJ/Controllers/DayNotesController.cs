using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DayNotesController(AppDbContext db) : ControllerBase
{
    // GET /api/daynotes?accountId=1&page=1&pageSize=20
    [HttpGet]
    public async Task<ActionResult<object>> GetAll(
        [FromQuery] int accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (pageSize > 100) pageSize = 100;

        var query = db.DayNotes
            .Where(n => n.AccountId == accountId)
            .OrderByDescending(n => n.Date);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new DayNoteDto(
                n.Id,
                n.Date.ToString("yyyy-MM-dd"),
                n.Content,
                n.UpdatedAt))
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    // PUT /api/daynotes/{date}?accountId=1
    [HttpPut("{date}")]
    public async Task<ActionResult<DayNoteDto>> Upsert(
        string date,
        [FromQuery] int accountId,
        [FromBody] SaveDayNoteRequest body)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
            return BadRequest("Invalid date format. Use yyyy-MM-dd.");

        var note = await db.DayNotes
            .FirstOrDefaultAsync(n => n.AccountId == accountId && n.Date == parsedDate);

        if (note == null)
        {
            note = new DayNote
            {
                AccountId = accountId,
                Date = parsedDate,
                Content = body.Content,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.DayNotes.Add(note);
        }
        else
        {
            note.Content = body.Content;
            note.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new DayNoteDto(note.Id, note.Date.ToString("yyyy-MM-dd"), note.Content, note.UpdatedAt));
    }

    // DELETE /api/daynotes/{date}?accountId=1
    [HttpDelete("{date}")]
    public async Task<IActionResult> Delete(string date, [FromQuery] int accountId)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
            return BadRequest("Invalid date format. Use yyyy-MM-dd.");

        var note = await db.DayNotes
            .FirstOrDefaultAsync(n => n.AccountId == accountId && n.Date == parsedDate);

        if (note == null) return NoContent();

        db.DayNotes.Remove(note);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
