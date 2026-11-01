using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface ISupplierLedgerService
{
    Task<Result<decimal>> GetSupplierBalanceAsync(int supplierId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<Dictionary<int, decimal>>> GetSupplierBalancesAsync(IEnumerable<int> supplierIds, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<SupplierLedgerReportDto>> GetSupplierLedgerReportAsync(int supplierId, DateTime fromDate, DateTime toDate, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportLedgerCsvAsync(int supplierId, DateTime fromDate, DateTime toDate, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportLedgerPdfAsync(int supplierId, DateTime fromDate, DateTime toDate, string? userRole, CancellationToken cancellationToken = default);
}

