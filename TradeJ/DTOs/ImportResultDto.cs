namespace TradeJ.DTOs;

public record ImportResultDto(
    int Imported,
    int Skipped,
    int Errors,
    List<string> ErrorMessages);
