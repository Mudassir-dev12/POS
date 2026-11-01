using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IStockAdjustmentRepository
{
    Task<PagedResult<StockAdjustmentListDto>> GetAdjustmentsAsync(DateTime? fromDate, DateTime? toDate, StockAdjustmentStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<StockAdjustment?> GetAdjustmentByIdAsync(int id, bool includeItems = true, CancellationToken cancellationToken = default);
    Task<StockAdjustmentDetailDto?> GetAdjustmentDetailAsync(int id, CancellationToken cancellationToken = default);
    Task<StockAdjustment> CreateAdjustmentAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default);
    Task UpdateAdjustmentAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default);
    Task SoftDeleteAdjustmentAsync(int id, CancellationToken cancellationToken = default);
    Task AddStockMovementAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

