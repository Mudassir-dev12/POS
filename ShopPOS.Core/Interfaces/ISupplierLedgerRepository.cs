using ShopPOS.Core.DTOs;

namespace ShopPOS.Core.Interfaces;

public interface ISupplierLedgerRepository
{
    Task<decimal> GetSupplierBalanceAsync(int supplierId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, decimal>> GetSupplierBalancesAsync(IEnumerable<int> supplierIds, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalPayableBalanceAsync(CancellationToken cancellationToken = default);
    Task<SupplierLedgerReportDto> GetSupplierLedgerReportAsync(int supplierId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}

