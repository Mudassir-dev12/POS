using ShopPOS.Core.DTOs;

namespace ShopPOS.Core.Interfaces;

public interface ISalesExportService
{
    Task<byte[]> ExportSalesToCsvAsync(IReadOnlyList<SaleSummaryDto> sales, CancellationToken cancellationToken = default);
}

