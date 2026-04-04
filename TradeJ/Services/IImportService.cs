using TradeJ.DTOs;

namespace TradeJ.Services;

public interface IImportService
{
    Task<ImportResultDto> ImportAsync(int accountId, Stream fileStream, string fileName);
}
