using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.DTOs;
using TradeJ.Models;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagCategoriesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TagCategoryDto>>> GetAll()
    {
        var categories = await db.TagCategories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Include(c => c.Tags)
                .ThenInclude(t => t.TradeTags)
            .ToListAsync();

        return Ok(categories.Select(MapCategory).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TagCategoryDto>> Create([FromBody] CreateTagCategoryDto dto)
    {
        var category = new TagCategory
        {
            Name = dto.Name,
            Color = dto.Color,
            SortOrder = await db.TagCategories.CountAsync()
        };
        db.TagCategories.Add(category);
        await db.SaveChangesAsync();
        await db.Entry(category).Collection(c => c.Tags).LoadAsync();
        return CreatedAtAction(nameof(GetAll), MapCategory(category));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTagCategoryDto dto)
    {
        var category = await db.TagCategories.FindAsync(id);
        if (category is null) return NotFound();

        category.Name = dto.Name;
        category.Color = dto.Color;
        category.SortOrder = dto.SortOrder;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await db.TagCategories.FindAsync(id);
        if (category is null) return NotFound();

        db.TagCategories.Remove(category);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{categoryId:int}/tags")]
    public async Task<ActionResult<TagDto>> CreateTag(int categoryId, [FromBody] CreateTagDto dto)
    {
        var category = await db.TagCategories.FindAsync(categoryId);
        if (category is null) return NotFound();

        var tag = new Tag { CategoryId = categoryId, Name = dto.Name };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return Ok(new TagDto(tag.Id, tag.CategoryId, tag.Name, 0));
    }

    [HttpPut("{categoryId:int}/tags/{tagId:int}")]
    public async Task<IActionResult> RenameTag(int categoryId, int tagId, [FromBody] RenameTagDto dto)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId && t.CategoryId == categoryId);
        if (tag is null) return NotFound();

        tag.Name = dto.Name;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{categoryId:int}/tags/{tagId:int}")]
    public async Task<IActionResult> DeleteTag(int categoryId, int tagId)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId && t.CategoryId == categoryId);
        if (tag is null) return NotFound();

        db.Tags.Remove(tag);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static TagCategoryDto MapCategory(TagCategory c) => new(
        c.Id,
        c.Name,
        c.Color,
        c.SortOrder,
        c.Tags.Select(t => new TagDto(t.Id, t.CategoryId, t.Name, t.TradeTags?.Count ?? 0)).ToList()
    );
}
