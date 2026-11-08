using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IPurchaseService
{
    Task<Result<PagedResult<PurchaseListDto>>> GetPurchasesAsync(PurchaseSearchQuery query, CancellationToken cancellationToken = default);
    Task<Result<PurchaseDetailDto>> GetPurchaseByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PurchaseDetailDto>> CreatePurchaseDraftAsync(CreatePurchaseDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<Result<PurchaseDetailDto>> UpdatePurchaseDraftAsync(UpdatePurchaseDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeletePurchaseDraftAsync(int id, int? userId, CancellationToken cancellationToken = default);
    Task<Result<PurchaseDetailDto>> PostPurchaseAsync(int purchaseId, decimal? payNowAmount, PaymentMethod? payNowMethod, int? userId, CancellationToken cancellationToken = default);
    Task<Result<bool>> CancelPurchaseAsync(int purchaseId, string reason, int? userId, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportPurchasePdfAsync(int purchaseId, CancellationToken cancellationToken = default);
    Task<Result<decimal>> GetProductCurrentStockAsync(int productId, CancellationToken cancellationToken = default);
    Task<Result<decimal>> GetProductLastCostAsync(int productId, CancellationToken cancellationToken = default);
    Task<BulkOperationResult> BulkDeleteDraftsAsync(IEnumerable<int> ids, int? userId, CancellationToken cancellationToken = default);
    Task<BulkOperationResult> BulkCancelPurchasesAsync(IEnumerable<int> ids, string reason, int? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetFilteredPurchaseIdsAsync(PurchaseSearchQuery query, CancellationToken cancellationToken = default);
}

