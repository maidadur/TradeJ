namespace TradeJ.DTOs;

public record TagDto(int Id, int CategoryId, string Name, int UsageCount);

public record TagCategoryDto(
    int Id,
    string Name,
    string Color,
    int SortOrder,
    List<TagDto> Tags);

public record CreateTagCategoryDto(string Name, string Color = "#6366f1");

public record UpdateTagCategoryDto(string Name, string Color, int SortOrder);

public record CreateTagDto(int CategoryId, string Name);

public record RenameTagDto(string Name);

