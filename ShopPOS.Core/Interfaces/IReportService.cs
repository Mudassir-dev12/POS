using System.Threading;
using System.Threading.Tasks;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.DTOs.Reports;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Interfaces;

public interface IReportService
{
    Task<DailySalesSummaryReportDto> GetDailySalesSummaryAsync(DateRangeDto dateRange, CancellationToken cancellationToken = default);

    Task<SalesByProductReportDto> GetSalesByProductAsync(DateRangeDto dateRange, SalesGroupBy groupBy = SalesGroupBy.Product, int? topN = null, string? sortBy = null, bool sortDescending = true, CancellationToken cancellationToken = default);

    Task<ProfitAndLossReportDto> GetProfitAndLossAsync(DateRangeDto dateRange, CancellationToken cancellationToken = default);

    Task<PurchasesReportDto> GetPurchasesReportAsync(DateRangeDto dateRange, CancellationToken cancellationToken = default);

    Task<TaxReportDto> GetTaxReportAsync(DateRangeDto dateRange, CancellationToken cancellationToken = default);

    Task<StockReportDto> GetStockMovementSummaryAsync(DateRangeDto dateRange, int slowMovingDays = 30, int fastMovingTopN = 10, CancellationToken cancellationToken = default);

    Task<ExpensesReportDto> GetExpensesReportAsync(DateRangeDto dateRange, CancellationToken cancellationToken = default);

    Task<PaymentMethodsReportDto> GetPaymentMethodsReportAsync(DateRangeDto dateRange, CancellationToken cancellationToken = default);

    Task<AuditLogReportDto> GetAuditLogsAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);

    Task<BarcodeDataQualityReportDto> GetBarcodeDataQualityAsync(CancellationToken cancellationToken = default);

    Task<DashboardReportSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);

    byte[] ExportReportToCsv(string reportType, object reportData);

    byte[] ExportReportToPdf(string reportType, object reportData, ShopSettingDto settings);

    IReadOnlyList<byte[]> RenderReportPageImages(string reportType, object reportData, ShopSettingDto settings);
}
