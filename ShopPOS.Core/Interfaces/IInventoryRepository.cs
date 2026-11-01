using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IInventoryRepository
{
    Task<PagedResult<StockOverviewItemDto>> GetStockOverviewAsync(StockOverviewFilterQuery query, bool isCashier, CancellationToken cancellationToken = default);
    Task<StockOverviewSummaryDto> GetStockOverviewSummaryAsync(bool isCashier, CancellationToken cancellationToken = default);
    Task<PagedResult<MovementHistoryItemDto>> GetMovementHistoryAsync(MovementHistoryFilterQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MovementHistoryItemDto>> GetAllMovementHistoryAsync(MovementHistoryFilterQuery query, CancellationToken cancellationToken = default);
    Task<(int purchasesCount, decimal purchasesTotal)> GetPurchasesThisMonthStatsAsync(CancellationToken cancellationToken = default);
}

