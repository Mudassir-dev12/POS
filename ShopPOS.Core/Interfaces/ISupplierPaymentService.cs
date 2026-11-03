using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface ISupplierPaymentService
{
    Task<Result<PagedResult<SupplierPaymentDto>>> GetPaymentsAsync(int? supplierId, int pageNumber, int pageSize, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<SupplierPaymentDto>> RecordPaymentAsync(CreateSupplierPaymentDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<bool>> CancelPaymentAsync(int paymentId, string reason, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<BulkOperationResult> BulkCancelPaymentsAsync(IEnumerable<int> ids, string reason, int? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetFilteredPaymentIdsAsync(int? supplierId, CancellationToken cancellationToken = default);
}

