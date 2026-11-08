using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IPurchaseReturnService
{
    Task<Result<PagedResult<PurchaseReturnDto>>> GetReturnsAsync(int? supplierId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<PurchaseReturnDto>> PostReturnAsync(CreatePurchaseReturnDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<Result<bool>> CancelPurchaseReturnAsync(int returnId, string reason, int? userId, CancellationToken cancellationToken = default);
    Task<BulkOperationResult> BulkCancelReturnsAsync(IEnumerable<int> ids, string reason, int? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetFilteredReturnIdsAsync(int? supplierId, CancellationToken cancellationToken = default);
}

