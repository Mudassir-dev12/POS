using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IPurchaseRepository
{
    Task<PagedResult<PurchaseListDto>> GetPurchasesAsync(PurchaseSearchQuery query, CancellationToken cancellationToken = default);
    Task<Purchase?> GetPurchaseByIdAsync(int id, bool includeItems = true, CancellationToken cancellationToken = default);
    Task<PurchaseDetailDto?> GetPurchaseDetailAsync(int id, CancellationToken cancellationToken = default);
    Task<Purchase> CreatePurchaseAsync(Purchase purchase, CancellationToken cancellationToken = default);
    Task UpdatePurchaseAsync(Purchase purchase, CancellationToken cancellationToken = default);
    Task SoftDeletePurchaseAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> IsSupplierInvoiceNoUsedAsync(int supplierId, string supplierInvoiceNo, int? excludePurchaseId = null, CancellationToken cancellationToken = default);
    Task<decimal> GetProductCurrentStockAsync(int productId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, decimal>> GetProductsCurrentStockAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);
    Task<decimal> GetProductLastCostAsync(int productId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, decimal>> GetProductsLastCostAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetProductsByIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);
    Task AddStockMovementAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task AddSupplierPaymentAsync(SupplierPayment payment, CancellationToken cancellationToken = default);
    Task UpdateProductCostPriceAsync(int productId, decimal newCostPrice, decimal oldCostPrice, int? userId, CancellationToken cancellationToken = default);
    Task<bool> HasActivePaymentsForPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveReturnsForPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalReturnedForPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
