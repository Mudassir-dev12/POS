using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IInventoryService
{
    Task<Result<PagedResult<StockOverviewItemDto>>> GetStockOverviewAsync(StockOverviewFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<StockOverviewSummaryDto>> GetStockOverviewSummaryAsync(CancellationToken cancellationToken = default);
    Task<Result<StockOverviewItemDto>> GetProductStockDetailAsync(int productId, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<MovementHistoryItemDto>>> GetMovementHistoryAsync(MovementHistoryFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CategoryValuationDto>>> GetCategoryValuationsAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ReorderItemDto>>> GetReorderListAsync(CancellationToken cancellationToken = default);
    Task<Result<PurchaseDetailDto>> CreatePurchaseDraftFromReorderAsync(CreatePurchaseFromReorderDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportStockOverviewCsvAsync(StockOverviewFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportStockOverviewPdfAsync(StockOverviewFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportMovementHistoryCsvAsync(MovementHistoryFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportValuationPdfAsync(CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportValuationCsvAsync(CancellationToken cancellationToken = default);
    Task<Result<DashboardStatsDto>> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
}

