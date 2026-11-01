using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface ISupplierPaymentRepository
{
    Task<PagedResult<SupplierPaymentDto>> GetPaymentsAsync(int? supplierId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierPaymentDto>> GetPaymentsByPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default);
    Task<SupplierPayment?> GetPaymentByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SupplierPayment> CreatePaymentAsync(SupplierPayment payment, CancellationToken cancellationToken = default);
    Task CancelPaymentAsync(int paymentId, string reason, int userId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalPaidForPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default);
    Task UpdatePurchasePaidAmountAsync(int purchaseId, decimal newPaidAmount, CancellationToken cancellationToken = default);
}
