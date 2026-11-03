using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IPurchaseReturnRepository
{
    Task<PagedResult<PurchaseReturnDto>> GetReturnsAsync(int? supplierId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PurchaseReturn?> GetReturnByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PurchaseReturn> CreateReturnAsync(PurchaseReturn purchaseReturn, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalReturnedQuantityAsync(int purchaseId, int productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetProductsByIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);
    Task AddStockMovementAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetFilteredIdsAsync(int? supplierId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
