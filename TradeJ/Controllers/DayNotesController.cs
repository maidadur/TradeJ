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
    // GET /api/daynotes?page=1&pageSize=500
    [HttpGet]
    public async Task<ActionResult<object>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (pageSize > 500) pageSize = 500;

        var query = db.DayNotes
            .OrderByDescending(n => n.Date);

        var total = await query.CountAsync();
        var notes = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dates = notes.Select(n => n.Date).ToList();
        var tagMap = await db.DayTags
            .Where(dt => dates.Contains(dt.Date))
            .GroupBy(dt => dt.Date)
            .ToDictionaryAsync(g => g.Key, g => g.Select(dt => dt.DayTagDefId).ToList());

        var items = notes.Select(n => new DayNoteDto(
            n.Id,
            n.Date.ToString("yyyy-MM-dd"),
            n.Content,
            n.UpdatedAt,
            tagMap.TryGetValue(n.Date, out var ids) ? ids : []));

        return Ok(new { total, page, pageSize, items });
    }

    // PUT /api/daynotes/{date}
    [HttpPut("{date}")]
    public async Task<ActionResult<DayNoteDto>> Upsert(
        string date,
        [FromBody] SaveDayNoteRequest body)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
            return BadRequest("Invalid date format. Use yyyy-MM-dd.");

        var note = await db.DayNotes
            .FirstOrDefaultAsync(n => n.Date == parsedDate);

        if (note == null)
        {
            note = new DayNote
            {
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

        var tagIds = await db.DayTags
            .Where(dt => dt.Date == parsedDate)
            .Select(dt => dt.DayTagDefId)
            .ToListAsync();

        return Ok(new DayNoteDto(note.Id, note.Date.ToString("yyyy-MM-dd"), note.Content, note.UpdatedAt, tagIds));
    }

    // DELETE /api/daynotes/{date}
    [HttpDelete("{date}")]
    public async Task<IActionResult> Delete(string date)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
            return BadRequest("Invalid date format. Use yyyy-MM-dd.");

        var note = await db.DayNotes
            .FirstOrDefaultAsync(n => n.Date == parsedDate);

        if (note == null) return NoContent();

        db.DayNotes.Remove(note);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/daynotes/{date}/tags/{dayTagDefId}
    [HttpPost("{date}/tags/{dayTagDefId:int}")]
    public async Task<IActionResult> AddTag(string date, int dayTagDefId)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
            return BadRequest("Invalid date format.");

        var def = await db.DayTagDefs.FindAsync(dayTagDefId);
        if (def is null) return NotFound("Day tag not found.");

        var exists = await db.DayTags.AnyAsync(dt => dt.Date == parsedDate && dt.DayTagDefId == dayTagDefId);
        if (!exists)
        {
            db.DayTags.Add(new DayTag { Date = parsedDate, DayTagDefId = dayTagDefId });
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    // DELETE /api/daynotes/{date}/tags/{dayTagDefId}
    [HttpDelete("{date}/tags/{dayTagDefId:int}")]
    public async Task<IActionResult> RemoveTag(string date, int dayTagDefId)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
            return BadRequest("Invalid date format.");

        var dt = await db.DayTags.FirstOrDefaultAsync(dt => dt.Date == parsedDate && dt.DayTagDefId == dayTagDefId);
        if (dt is not null)
        {
            db.DayTags.Remove(dt);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }
}
