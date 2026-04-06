using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DayTagDefsController(AppDbContext db) : ControllerBase
{
    // GET /api/daytagdefs
    [HttpGet]
    public async Task<ActionResult<List<DayTagDefDto>>> GetAll()
    {
        var defs = await db.DayTagDefs
            .OrderBy(d => d.Name)
            .Select(d => new DayTagDefDto(d.Id, d.Name, d.Color))
            .ToListAsync();
        return Ok(defs);
    }

    // POST /api/daytagdefs
    [HttpPost]
    public async Task<ActionResult<DayTagDefDto>> Create([FromBody] SaveDayTagDefRequest body)
    {
        var def = new DayTagDef { Name = body.Name.Trim(), Color = body.Color };
        db.DayTagDefs.Add(def);
        await db.SaveChangesAsync();
        return Ok(new DayTagDefDto(def.Id, def.Name, def.Color));
    }

    // PUT /api/daytagdefs/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveDayTagDefRequest body)
    {
        var def = await db.DayTagDefs.FindAsync(id);
        if (def is null) return NotFound();
        def.Name = body.Name.Trim();
        def.Color = body.Color;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/daytagdefs/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var def = await db.DayTagDefs.FindAsync(id);
        if (def is null) return NotFound();
        db.DayTagDefs.Remove(def);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
