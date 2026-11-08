using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.DTOs.Reports;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Reports;

public class ReportMenuItem
{
    public string Key { get; }
    public string Title { get; }
    public string Icon { get; }
    public string Description { get; }

    public ReportMenuItem(string key, string title, string icon, string description)
    {
        Key = key;
        Title = title;
        Icon = icon;
        Description = description;
    }
}

public class ReportKpiCard
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Subtext { get; set; }
    public string? StatusBrushName { get; set; }
}

public partial class ReportsViewModel : ObservableObject
{
    private readonly IReportService _reportService;
    private readonly IShopSettingService _shopSettingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly IReceiptPrinterService? _receiptPrinterService;

    public ObservableCollection<ReportMenuItem> ReportMenuItems { get; } = new();

    [ObservableProperty]
    private ReportMenuItem _selectedReport;

    // Date Range Presets
    [ObservableProperty]
    private ReportDatePreset _selectedPreset = ReportDatePreset.ThisMonth;

    [ObservableProperty]
    private DateTime _dateFrom;

    [ObservableProperty]
    private DateTime _dateTo;

    [ObservableProperty]
    private bool _isCustomDate;

    // Report-specific filters
    public SalesGroupBy[] AvailableSalesGroupBy { get; } = Enum.GetValues<SalesGroupBy>();

    [ObservableProperty]
    private SalesGroupBy _selectedSalesGroupBy = SalesGroupBy.Product;

    [ObservableProperty]
    private int? _topN = null; // null = all

    [ObservableProperty]
    private string _purchasesSubView = "BySupplier"; // BySupplier, ByProduct, ByDay, PayablesAging

    [ObservableProperty]
    private string _stockSubView = "MovementSummary"; // MovementSummary, SlowMoving

    [ObservableProperty]
    private int _slowMovingDays = 30;

    [ObservableProperty]
    private int _fastMovingTopN = 10;

    [ObservableProperty]
    private string _expensesSubView = "ByCategory"; // ByCategory, ByDay

    [ObservableProperty]
    private string? _auditEntityFilter;

    [ObservableProperty]
    private string? _auditActionFilter;

    [ObservableProperty]
    private int _auditPageNumber = 1;

    [ObservableProperty]
    private int _auditTotalPages = 1;

    // State & Content
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasData;

    public ObservableCollection<ReportKpiCard> KpiCards { get; } = new();

    // Raw report data for exports
    public object? CurrentReportData { get; private set; }

    // Typed Report Results
    [ObservableProperty]
    private DailySalesSummaryReportDto? _dailySalesSummary;

    [ObservableProperty]
    private SalesByProductReportDto? _salesByProduct;

    [ObservableProperty]
    private ProfitAndLossReportDto? _profitAndLoss;

    [ObservableProperty]
    private PurchasesReportDto? _purchasesReport;

    [ObservableProperty]
    private TaxReportDto? _taxReport;

    [ObservableProperty]
    private StockReportDto? _stockReport;

    [ObservableProperty]
    private ExpensesReportDto? _expensesReport;

    [ObservableProperty]
    private PaymentMethodsReportDto? _paymentMethodsReport;

    [ObservableProperty]
    private AuditLogReportDto? _auditLogReport;

    [ObservableProperty]
    private BarcodeDataQualityReportDto? _barcodeQualityReport;

    public ReportsViewModel()
    {
        // Parameterless for UI Smoke Tests
        _reportService = null!;
        _shopSettingService = null!;
        _dialogService = null!;
        _navigationService = null!;
        SetupMenuItems();
        _selectedReport = ReportMenuItems[0];
        ApplyPreset(ReportDatePreset.ThisMonth);
    }

    public ReportsViewModel(
        IReportService reportService,
        IShopSettingService shopSettingService,
        IDialogService dialogService,
        INavigationService navigationService,
        IReceiptPrinterService? receiptPrinterService = null)
    {
        _reportService = reportService;
        _shopSettingService = shopSettingService;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _receiptPrinterService = receiptPrinterService;

        SetupMenuItems();
        _selectedReport = ReportMenuItems[0];
        ApplyPreset(ReportDatePreset.ThisMonth);
    }

    private void SetupMenuItems()
    {
        ReportMenuItems.Clear();
        ReportMenuItems.Add(new ReportMenuItem("DailySalesSummary", "Daily Sales Summary", "Calendar24", "Day-by-day sales, discounts, taxes, returns, net sales and payment methods"));
        ReportMenuItems.Add(new ReportMenuItem("SalesByProduct", "Sales Analysis", "ShoppingBag24", "Sales grouped by product, category, brand, hour or method"));
        ReportMenuItems.Add(new ReportMenuItem("ProfitAndLoss", "Profit and Loss", "Money24", "Net sales, COGS, gross profit, expenses, stock write-offs and net profit"));
        ReportMenuItems.Add(new ReportMenuItem("Purchases", "Purchases and Payables", "DocumentBulletList24", "Purchases by supplier, product, day, and supplier payables aging"));
        ReportMenuItems.Add(new ReportMenuItem("TaxReport", "Tax Report", "Receipt24", "Output tax from sales vs input tax from purchases by rate"));
        ReportMenuItems.Add(new ReportMenuItem("StockReports", "Stock Reports", "Box24", "Movement summary, slow-moving and dead stock"));
        ReportMenuItems.Add(new ReportMenuItem("Expenses", "Expenses Report", "MoneyCalculator24", "Operating expenses by category and by day"));
        ReportMenuItems.Add(new ReportMenuItem("PaymentMethods", "Payment Methods", "Payment24", "Money in vs money out per payment method"));
        ReportMenuItems.Add(new ReportMenuItem("AuditLogs", "Audit Log Viewer", "ShieldCheckmark24", "Forensic action and entity change audit logs"));
        ReportMenuItems.Add(new ReportMenuItem("BarcodeQuality", "Barcode & Data Quality", "Barcode24", "Missing or invalid barcodes, zero cost, negative stock warnings"));
    }

    partial void OnSelectedReportChanged(ReportMenuItem value)
    {
        _ = LoadReportAsync();
    }

    partial void OnSelectedSalesGroupByChanged(SalesGroupBy value)
    {
        if (SelectedReport?.Key == "SalesByProduct")
        {
            _ = LoadReportAsync();
        }
    }

    partial void OnPurchasesSubViewChanged(string value)
    {
        UpdatePurchasesKpis();
    }

    partial void OnStockSubViewChanged(string value)
    {
        UpdateStockKpis();
    }

    partial void OnExpensesSubViewChanged(string value)
    {
        UpdateExpensesKpis();
    }

    [RelayCommand]
    public void SelectPreset(string presetStr)
    {
        if (Enum.TryParse<ReportDatePreset>(presetStr, true, out var preset))
        {
            ApplyPreset(preset);
            _ = LoadReportAsync();
        }
    }

    public void ApplyPreset(ReportDatePreset preset)
    {
        SelectedPreset = preset;
        var now = DateTime.Now;
        var today = now.Date;

        switch (preset)
        {
            case ReportDatePreset.Today:
                DateFrom = today;
                DateTo = today.AddDays(1).AddTicks(-1);
                IsCustomDate = false;
                break;

            case ReportDatePreset.Yesterday:
                DateFrom = today.AddDays(-1);
                DateTo = today.AddTicks(-1);
                IsCustomDate = false;
                break;

            case ReportDatePreset.ThisWeek:
                int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                var monday = today.AddDays(-diff);
                DateFrom = monday;
                DateTo = monday.AddDays(7).AddTicks(-1);
                IsCustomDate = false;
                break;

            case ReportDatePreset.ThisMonth:
                DateFrom = new DateTime(today.Year, today.Month, 1);
                DateTo = DateFrom.AddMonths(1).AddTicks(-1);
                IsCustomDate = false;
                break;

            case ReportDatePreset.LastMonth:
                var firstDayOfLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                DateFrom = firstDayOfLastMonth;
                DateTo = firstDayOfLastMonth.AddMonths(1).AddTicks(-1);
                IsCustomDate = false;
                break;

            case ReportDatePreset.ThisYear:
                DateFrom = new DateTime(today.Year, 1, 1);
                DateTo = new DateTime(today.Year, 12, 31, 23, 59, 59, 999);
                IsCustomDate = false;
                break;

            case ReportDatePreset.Custom:
                IsCustomDate = true;
                break;
        }
    }

    [RelayCommand]
    public async Task LoadReportAsync()
    {
        if (_reportService == null || SelectedReport == null) return;

        IsLoading = true;
        ErrorMessage = null;
        KpiCards.Clear();
        HasData = false;

        var dateRange = DateRangeDto.FromPreset(SelectedPreset, null, DateFrom, DateTo);

        try
        {
            switch (SelectedReport.Key)
            {
                case "DailySalesSummary":
                    var dailyResult = await _reportService.GetDailySalesSummaryAsync(dateRange);
                    DailySalesSummary = dailyResult;
                    CurrentReportData = dailyResult;
                    HasData = dailyResult.Rows.Count > 0;
                    SetupDailySalesKpis(dailyResult);
                    break;

                case "SalesByProduct":
                    var salesResult = await _reportService.GetSalesByProductAsync(dateRange, SelectedSalesGroupBy, TopN);
                    SalesByProduct = salesResult;
                    CurrentReportData = salesResult;
                    HasData = salesResult.Rows.Count > 0;
                    SetupSalesByProductKpis(salesResult);
                    break;

                case "ProfitAndLoss":
                    var pnlResult = await _reportService.GetProfitAndLossAsync(dateRange);
                    ProfitAndLoss = pnlResult;
                    CurrentReportData = pnlResult;
                    HasData = true; // PnL always displays structure
                    SetupProfitAndLossKpis(pnlResult);
                    break;

                case "Purchases":
                    var purchasesResult = await _reportService.GetPurchasesReportAsync(dateRange);
                    PurchasesReport = purchasesResult;
                    CurrentReportData = purchasesResult;
                    HasData = purchasesResult.BySupplierRows.Count > 0 || purchasesResult.ByProductRows.Count > 0 || purchasesResult.AgingRows.Count > 0;
                    UpdatePurchasesKpis();
                    break;

                case "TaxReport":
                    var taxResult = await _reportService.GetTaxReportAsync(dateRange);
                    TaxReport = taxResult;
                    CurrentReportData = taxResult;
                    HasData = taxResult.OutputTaxRows.Count > 0 || taxResult.InputTaxRows.Count > 0;
                    SetupTaxKpis(taxResult);
                    break;

                case "StockReports":
                    var stockResult = await _reportService.GetStockMovementSummaryAsync(dateRange, SlowMovingDays, FastMovingTopN);
                    StockReport = stockResult;
                    CurrentReportData = stockResult;
                    HasData = stockResult.MovementSummaryRows.Count > 0 || stockResult.SlowMovingRows.Count > 0;
                    UpdateStockKpis();
                    break;

                case "Expenses":
                    var expResult = await _reportService.GetExpensesReportAsync(dateRange);
                    ExpensesReport = expResult;
                    CurrentReportData = expResult;
                    HasData = expResult.ByCategoryRows.Count > 0 || expResult.ByDayRows.Count > 0;
                    UpdateExpensesKpis();
                    break;

                case "PaymentMethods":
                    var payResult = await _reportService.GetPaymentMethodsReportAsync(dateRange);
                    PaymentMethodsReport = payResult;
                    CurrentReportData = payResult;
                    HasData = payResult.Rows.Count > 0;
                    SetupPaymentMethodsKpis(payResult);
                    break;

                case "AuditLogs":
                    var auditFilter = new AuditLogFilterDto
                    {
                        DateRange = dateRange,
                        EntityName = string.IsNullOrWhiteSpace(AuditEntityFilter) ? null : AuditEntityFilter.Trim(),
                        Action = string.IsNullOrWhiteSpace(AuditActionFilter) ? null : AuditActionFilter.Trim(),
                        PageIndex = AuditPageNumber - 1,
                        PageSize = 50
                    };
                    var auditResult = await _reportService.GetAuditLogsAsync(auditFilter);
                    AuditLogReport = auditResult;
                    CurrentReportData = auditResult;
                    AuditTotalPages = (int)Math.Ceiling((double)auditResult.TotalCount / Math.Max(1, auditResult.PageSize));
                    if (AuditTotalPages < 1) AuditTotalPages = 1;
                    HasData = auditResult.Rows.Count > 0;
                    SetupAuditKpis(auditResult);
                    break;

                case "BarcodeQuality":
                    var barcodeResult = await _reportService.GetBarcodeDataQualityAsync();
                    BarcodeQualityReport = barcodeResult;
                    CurrentReportData = barcodeResult;
                    HasData = barcodeResult.TotalIssueCount > 0;
                    SetupBarcodeKpis(barcodeResult);
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading report: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetupDailySalesKpis(DailySalesSummaryReportDto report)
    {
        KpiCards.Clear();
        KpiCards.Add(new ReportKpiCard { Label = "NET SALES", Value = $"Rs {report.TotalNetSales:N2}", Subtext = $"{report.TotalSalesCount} sales" });
        KpiCards.Add(new ReportKpiCard { Label = "GROSS SALES", Value = $"Rs {report.TotalGrossSales:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "DISCOUNTS", Value = $"Rs {report.TotalDiscounts:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "TAX COLLECTED", Value = $"Rs {report.TotalTax:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "RETURNS", Value = $"Rs {report.TotalReturns:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "AVG SALE", Value = $"Rs {report.OverallAverageSaleValue:N2}" });
    }

    private void SetupSalesByProductKpis(SalesByProductReportDto report)
    {
        KpiCards.Clear();
        KpiCards.Add(new ReportKpiCard { Label = "TOTAL REVENUE", Value = $"Rs {report.TotalNetRevenue:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "TOTAL QUANTITY", Value = report.TotalQuantitySold.ToString("N2") });
        KpiCards.Add(new ReportKpiCard { Label = "TOTAL PROFIT", Value = $"Rs {report.TotalProfit:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "AVG MARGIN", Value = $"{report.OverallMarginPercent:N1}%" });
    }

    private void SetupProfitAndLossKpis(ProfitAndLossReportDto report)
    {
        KpiCards.Clear();
        KpiCards.Add(new ReportKpiCard { Label = "NET SALES", Value = $"Rs {report.CurrentNetSales:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "COST OF GOODS", Value = $"Rs {report.CurrentCogs:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "GROSS PROFIT", Value = $"Rs {report.CurrentGrossProfit:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "OPERATING EXPENSES", Value = $"Rs {report.CurrentTotalExpenses:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "STOCK WRITE-OFFS", Value = $"Rs {report.CurrentStockWriteOffs:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "NET PROFIT", Value = $"Rs {report.CurrentNetProfit:N2}", StatusBrushName = report.CurrentNetProfit >= 0 ? "SuccessBrush" : "DangerBrush" });
    }

    private void UpdatePurchasesKpis()
    {
        if (PurchasesReport == null) return;
        KpiCards.Clear();
        KpiCards.Add(new ReportKpiCard { Label = "TOTAL PURCHASES", Value = $"Rs {PurchasesReport.TotalNetPurchases:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "TOTAL PAID", Value = $"Rs {PurchasesReport.TotalPaid:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "TOTAL PAYABLES", Value = $"Rs {PurchasesReport.TotalPayablesBalance:N2}" });
    }

    private void SetupTaxKpis(TaxReportDto report)
    {
        KpiCards.Clear();
        KpiCards.Add(new ReportKpiCard { Label = "OUTPUT TAX (SALES)", Value = $"Rs {report.TotalOutputTax:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "INPUT TAX (PURCHASES)", Value = $"Rs {report.TotalInputTax:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "NET TAX PAYABLE", Value = $"Rs {report.NetTaxPayable:N2}" });
    }

    private void UpdateStockKpis()
    {
        if (StockReport == null) return;
        KpiCards.Clear();
        decimal totalValuation = StockReport.SlowMovingRows.Sum(s => s.ValueTiedUp);
        KpiCards.Add(new ReportKpiCard { Label = "SLOW-MOVING VALUE", Value = $"Rs {totalValuation:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "TRACKED PRODUCTS", Value = StockReport.MovementSummaryRows.Count.ToString() });
    }

    private void UpdateExpensesKpis()
    {
        if (ExpensesReport == null) return;
        KpiCards.Clear();
        KpiCards.Add(new ReportKpiCard { Label = "TOTAL EXPENSES", Value = $"Rs {ExpensesReport.TotalExpenseAmount:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "CATEGORIES", Value = ExpensesReport.ByCategoryRows.Count.ToString() });
        KpiCards.Add(new ReportKpiCard { Label = "ACTIVE DAYS", Value = ExpensesReport.ByDayRows.Count.ToString() });
    }

    private void SetupPaymentMethodsKpis(PaymentMethodsReportDto report)
    {
        KpiCards.Clear();
        KpiCards.Add(new ReportKpiCard { Label = "TOTAL MONEY IN", Value = $"Rs {report.GrandTotalIn:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "TOTAL MONEY OUT", Value = $"Rs {report.GrandTotalOut:N2}" });
        KpiCards.Add(new ReportKpiCard { Label = "NET CASH FLOW", Value = $"Rs {report.NetTotalFlow:N2}" });
    }

    private void SetupAuditKpis(AuditLogReportDto report)
    {
        KpiCards.Clear();
        KpiCards.Add(new ReportKpiCard { Label = "AUDIT RECORDS", Value = report.TotalCount.ToString("N0") });
        KpiCards.Add(new ReportKpiCard { Label = "CURRENT PAGE", Value = $"{AuditPageNumber} of {AuditTotalPages}" });
    }

    private void SetupBarcodeKpis(BarcodeDataQualityReportDto report)
    {
        KpiCards.Clear();
        KpiCards.Add(new ReportKpiCard { Label = "MISSING BARCODES", Value = report.MissingBarcodeCount.ToString() });
        KpiCards.Add(new ReportKpiCard { Label = "DUPLICATE BARCODES", Value = report.DuplicateBarcodeCount.ToString() });
        KpiCards.Add(new ReportKpiCard { Label = "ZERO COST ITEMS", Value = report.ZeroCostPriceCount.ToString() });
        KpiCards.Add(new ReportKpiCard { Label = "SALE BELOW COST", Value = report.PriceBelowCostCount.ToString() });
        KpiCards.Add(new ReportKpiCard { Label = "NEGATIVE STOCK", Value = report.NegativeStockCount.ToString() });
    }

    private async Task<ShopSettingDto> GetShopSettingDtoAsync()
    {
        var s = await _shopSettingService.GetSettingsAsync();
        return new ShopSettingDto
        {
            ShopName = s.ShopName,
            Address = s.Address,
            Phone = s.Phone,
            TaxNumber = s.TaxNumber,
            CurrencySymbol = s.CurrencySymbol,
            ReceiptFooterText = s.ReceiptFooterText
        };
    }

    [RelayCommand]
    public void ExportCsv()
    {
        if (CurrentReportData == null || SelectedReport == null)
        {
            _dialogService?.ShowMessage("No data available to export.", "Export CSV");
            return;
        }

        try
        {
            var fileName = $"{SelectedReport.Key}_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            var savePath = _dialogService?.ShowSaveFileDialog("CSV Document (*.csv)|*.csv", fileName, "Export CSV");
            if (string.IsNullOrWhiteSpace(savePath)) return;

            var bytes = _reportService.ExportReportToCsv(SelectedReport.Key, CurrentReportData);
            File.WriteAllBytes(savePath, bytes);
            _dialogService?.ShowMessage($"Report exported successfully to {Path.GetFileName(savePath)}.", "Export Successful");
        }
        catch (Exception ex)
        {
            _dialogService?.ShowMessage($"Export failed: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        if (CurrentReportData == null || SelectedReport == null || _shopSettingService == null)
        {
            _dialogService?.ShowMessage("No data available to export.", "Export PDF");
            return;
        }

        try
        {
            var fileName = $"{SelectedReport.Key}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            var savePath = _dialogService?.ShowSaveFileDialog("PDF Document (*.pdf)|*.pdf", fileName, "Export PDF");
            if (string.IsNullOrWhiteSpace(savePath)) return;

            var settings = await GetShopSettingDtoAsync();
            var bytes = _reportService.ExportReportToPdf(SelectedReport.Key, CurrentReportData, settings);
            File.WriteAllBytes(savePath, bytes);
            _dialogService?.ShowMessage($"Report exported successfully to {Path.GetFileName(savePath)}.", "Export Successful");
        }
        catch (Exception ex)
        {
            _dialogService?.ShowMessage($"Export failed: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public async Task PrintPreviewAsync()
    {
        if (CurrentReportData == null || SelectedReport == null || _shopSettingService == null)
        {
            _dialogService?.ShowMessage("No data available for print preview.", "Print Preview");
            return;
        }

        try
        {
            var settings = await GetShopSettingDtoAsync();
            var pdfBytes = _reportService.ExportReportToPdf(SelectedReport.Key, CurrentReportData, settings);
            var pageImages = _reportService.RenderReportPageImages(SelectedReport.Key, CurrentReportData, settings);

            var previewVm = new ReportPreviewDialogViewModel(
                SelectedReport.Title,
                pdfBytes,
                pageImages,
                _receiptPrinterService,
                _shopSettingService);

            _dialogService.ShowReportPreviewDialog(previewVm);
        }
        catch (Exception ex)
        {
            _dialogService?.ShowMessage($"Print preview error: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public void DrillDown(object? row)
    {
        if (row == null) return;

        if (row is DailySalesSummaryRowDto dailyRow)
        {
            _navigationService?.NavigateTo("SalesList");
        }
        else if (row is SalesByProductRowDto salesRow && salesRow.ProductId.HasValue)
        {
            _navigationService?.NavigateTo("Products", salesRow.ProductId.Value);
        }
        else if (row is PurchaseSupplierRowDto suppRow)
        {
            _navigationService?.NavigateTo("SupplierLedger", suppRow.SupplierId);
        }
        else if (row is SupplierPayablesAgingRowDto payRow)
        {
            _navigationService?.NavigateTo("SupplierLedger", payRow.SupplierId);
        }
        else if (row is StockMovementSummaryRowDto moveRow)
        {
            _navigationService?.NavigateTo("Products", moveRow.ProductId);
        }
        else if (row is DataQualityIssueDto qualRow)
        {
            _navigationService?.NavigateTo("Products", qualRow.ProductId);
        }
    }

    [RelayCommand]
    public async Task NextAuditPageAsync()
    {
        if (AuditPageNumber < AuditTotalPages)
        {
            AuditPageNumber++;
            await LoadReportAsync();
        }
    }

    [RelayCommand]
    public async Task PrevAuditPageAsync()
    {
        if (AuditPageNumber > 1)
        {
            AuditPageNumber--;
            await LoadReportAsync();
        }
    }
}
