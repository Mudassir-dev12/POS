using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IStockAdjustmentService
{
    Task<Result<PagedResult<StockAdjustmentListDto>>> GetAdjustmentsAsync(DateTime? fromDate, DateTime? toDate, StockAdjustmentStatus? status, int pageNumber, int pageSize, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<StockAdjustmentDetailDto>> GetAdjustmentByIdAsync(int id, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<StockAdjustmentDetailDto>> CreateAdjustmentDraftAsync(CreateStockAdjustmentDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<StockAdjustmentDetailDto>> UpdateAdjustmentDraftAsync(UpdateStockAdjustmentDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAdjustmentDraftAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<StockAdjustmentDetailDto>> PostAdjustmentAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<bool>> CancelAdjustmentAsync(int id, string reason, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<bool>> CreateAndPostSingleAdjustmentAsync(int productId, decimal targetQuantity, string reasonText, StockAdjustmentReason reasonType, int? userId, string? userRole, CancellationToken cancellationToken = default);
}

