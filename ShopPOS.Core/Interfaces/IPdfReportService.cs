using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IPdfReportService
{
    Task<byte[]> GeneratePurchaseInvoicePdfAsync(PurchaseDetailDto purchase, ShopSettingDto settings, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateSupplierLedgerPdfAsync(SupplierLedgerReportDto ledger, ShopSettingDto settings, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateStockOverviewPdfAsync(IReadOnlyList<StockOverviewItemDto> items, ShopSettingDto settings, bool isCashier = false, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateValuationPdfAsync(IReadOnlyList<CategoryValuationDto> valuations, ShopSettingDto settings, CancellationToken cancellationToken = default);

    Task<byte[]> GenerateSaleReceiptPdfAsync(SaleDetailDto sale, ShopSettingDto settings, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateSalesListPdfAsync(IReadOnlyList<SaleSummaryDto> sales, ShopSettingDto settings, CancellationToken cancellationToken = default);

    // Phase 5C
    Task<byte[]> GenerateReceiptPdfAsync(ReceiptDocument document, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateA4InvoicePdfAsync(ReceiptDocument document, CancellationToken cancellationToken = default);
    IReadOnlyList<byte[]> RenderDocumentPageImages(ReceiptDocument document);
}
