using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IInventoryService
{
    Task<Result<PagedResult<StockOverviewItemDto>>> GetStockOverviewAsync(StockOverviewFilterQuery query, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<StockOverviewSummaryDto>> GetStockOverviewSummaryAsync(string? userRole, CancellationToken cancellationToken = default);
    Task<Result<StockOverviewItemDto>> GetProductStockDetailAsync(int productId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<MovementHistoryItemDto>>> GetMovementHistoryAsync(MovementHistoryFilterQuery query, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CategoryValuationDto>>> GetCategoryValuationsAsync(string? userRole, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ReorderItemDto>>> GetReorderListAsync(string? userRole, CancellationToken cancellationToken = default);
    Task<Result<PurchaseDetailDto>> CreatePurchaseDraftFromReorderAsync(CreatePurchaseFromReorderDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportStockOverviewCsvAsync(StockOverviewFilterQuery query, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportStockOverviewPdfAsync(StockOverviewFilterQuery query, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportMovementHistoryCsvAsync(MovementHistoryFilterQuery query, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportValuationPdfAsync(string? userRole, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportValuationCsvAsync(string? userRole, CancellationToken cancellationToken = default);
    Task<Result<DashboardStatsDto>> GetDashboardStatsAsync(string? userRole, CancellationToken cancellationToken = default);
}

