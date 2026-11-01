using ShopPOS.Core.DTOs;

namespace ShopPOS.Core.Interfaces;

public interface IPdfReportService
{
    Task<byte[]> GeneratePurchaseInvoicePdfAsync(PurchaseDetailDto purchase, ShopSettingDto settings, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateSupplierLedgerPdfAsync(SupplierLedgerReportDto ledger, ShopSettingDto settings, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateStockOverviewPdfAsync(IReadOnlyList<StockOverviewItemDto> items, ShopSettingDto settings, bool isCashier, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateValuationPdfAsync(IReadOnlyList<CategoryValuationDto> valuations, ShopSettingDto settings, CancellationToken cancellationToken = default);
}

