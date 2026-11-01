using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class SalesViewModel : ObservableObject
{
    private readonly ISaleService _saleService;
    private readonly ISaleReturnService _saleReturnService;
    private readonly ICustomerService _customerService;
    private readonly IShopSettingService _shopSettingService;
    private readonly IPdfReportService _pdfReportService;
    private readonly ISalesExportService _salesExportService;
    private readonly IReceiptPrinterService _receiptPrinterService;
    private readonly IDialogService _dialogService;
    private readonly IReceiptBuilder? _receiptBuilder;
    private readonly IAuditLogService? _auditLogService;
    private readonly ICurrentUserProvider? _currentUserProvider;

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private DateTime? _dateFrom = DateTime.Today;

    [ObservableProperty]
    private DateTime? _dateTo = DateTime.Today;

    [ObservableProperty]
    private int? _selectedCustomerId;

    [ObservableProperty]
    private SaleStatus? _selectedStatus;

    [ObservableProperty]
    private bool _withDueOnly;

    [ObservableProperty]
    private int _pageNumber = 1;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _totalPages = 1;

    // Filtered range summary totals
    [ObservableProperty]
    private decimal _filteredGrandTotal;

    [ObservableProperty]
    private decimal _filteredPaidAmount;

    [ObservableProperty]
    private decimal _filteredDueAmount;

    [ObservableProperty]
    private SaleSummaryDto? _selectedItem;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _currencySymbol = "$";

    public ObservableCollection<SaleSummaryDto> Items { get; } = new();
    public ObservableCollection<CustomerDto> Customers { get; } = new();

    public SalesViewModel(
        ISaleService saleService,
        ISaleReturnService saleReturnService,
        ICustomerService customerService,
        IShopSettingService shopSettingService,
        IPdfReportService pdfReportService,
        ISalesExportService salesExportService,
        IReceiptPrinterService receiptPrinterService,
        IDialogService dialogService,
        IReceiptBuilder? receiptBuilder = null,
        IAuditLogService? auditLogService = null,
        ICurrentUserProvider? currentUserProvider = null)
    {
        _saleService = saleService;
        _saleReturnService = saleReturnService;
        _customerService = customerService;
        _shopSettingService = shopSettingService;
        _pdfReportService = pdfReportService;
        _salesExportService = salesExportService;
        _receiptPrinterService = receiptPrinterService;
        _dialogService = dialogService;
        _receiptBuilder = receiptBuilder;
        _auditLogService = auditLogService;
        _currentUserProvider = currentUserProvider;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        await LoadSettingsAndCustomersAsync();
        await LoadSalesAsync();
    }

    public async Task InitializeWithCustomerFilterAsync(int customerId)
    {
        SelectedCustomerId = customerId;
        DateFrom = null;
        DateTo = null;
        await InitializeAsync();
    }

    private async Task LoadSettingsAndCustomersAsync()
    {
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            CurrencySymbol = settings.CurrencySymbol;

            Customers.Clear();
            var custResult = await _customerService.GetPagedCustomersAsync(new MasterDataSearchQuery { PageSize = 500, IsActive = true });
            if (custResult.Success && custResult.Data != null)
            {
                foreach (var c in custResult.Data.Items)
                {
                    Customers.Add(c);
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task LoadSalesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Items.Clear();

            DateTime? from = DateFrom?.Date;
            DateTime? to = DateTo?.Date.AddDays(1).AddTicks(-1);

            var criteria = new SaleSearchDto
            {
                SearchTerm = SearchTerm?.Trim(),
                DateFrom = from,
                DateTo = to,
                CustomerId = SelectedCustomerId,
                Status = SelectedStatus,
                PageNumber = PageNumber,
                PageSize = PageSize
            };

            var paged = await _saleService.SearchSalesAsync(criteria);
            var resultList = paged.Items;

            if (WithDueOnly)
            {
                resultList = resultList.Where(s => s.DueAmount > 0).ToList();
            }

            foreach (var item in resultList)
            {
                Items.Add(item);
            }

            TotalCount = WithDueOnly ? resultList.Count : paged.TotalCount;
            TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
            if (TotalPages < 1) TotalPages = 1;

            FilteredGrandTotal = resultList.Sum(s => s.GrandTotal);
            FilteredPaidAmount = resultList.Sum(s => s.PaidAmount);
            FilteredDueAmount = resultList.Sum(s => s.DueAmount);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void OpenDetails() => OpenSale();

    [RelayCommand]
    public void OpenSale()
    {
        if (SelectedItem == null)
        {
            _dialogService.ShowMessage("Please select a sale from the list to view details.", "View Sale");
            return;
        }

        var vm = new SaleDetailDialogViewModel(
            SelectedItem.Id,
            _saleService,
            _pdfReportService,
            _shopSettingService,
            _receiptPrinterService,
            _receiptBuilder,
            _auditLogService,
            _currentUserProvider,
            _dialogService);
        _dialogService.ShowSaleDetailDialog(vm);
    }

    [RelayCommand]
    public async Task ReprintSaleAsync()
    {
        if (SelectedItem == null) return;

        var saleResult = await _saleService.GetSaleByIdAsync(SelectedItem.Id);
        if (saleResult.Success && saleResult.Data != null)
        {
            var sale = saleResult.Data;
            var settings = await _shopSettingService.GetSettingsAsync();

            if (_auditLogService != null && _currentUserProvider != null)
            {
                await _auditLogService.LogAsync(
                    _currentUserProvider.UserId,
                    "ReprintReceipt",
                    "Sale",
                    sale.Id,
                    $"Reprinted receipt for {sale.SaleNumber} (marked DUPLICATE)");
            }

            var printResult = await _receiptPrinterService.PrintSaleReceiptAsync(sale, settings, isDuplicate: true);

            if (printResult.Success)
            {
                if (settings.PrinterMode == Core.Enums.PrinterMode.PdfPreview && printResult.PdfBytes != null && _receiptBuilder != null)
                {
                    var doc = _receiptBuilder.BuildFromSale(sale, settings, isDuplicate: true);
                    var previewVm = new ReceiptPreviewDialogViewModel(doc, printResult.PdfBytes, _pdfReportService, _receiptPrinterService, _shopSettingService);
                    _dialogService.ShowReceiptPreviewDialog(previewVm);
                }
                else
                {
                    _dialogService.ShowMessage(printResult.NoticeMessage ?? $"Receipt print job sent for '{sale.SaleNumber}'.", "Receipt Reprint");
                }
            }
            else
            {
                _dialogService.ShowMessage($"Receipt could not be printed: {printResult.ErrorMessage}", "Print Notice");
            }
        }
    }

    [RelayCommand]
    public async Task CancelSaleAsync()
    {
        if (SelectedItem == null) return;

        if (SelectedItem.Status == SaleStatus.Cancelled)
        {
            _dialogService.ShowMessage($"Sale '{SelectedItem.SaleNumber}' is already cancelled.", "Cancel Sale");
            return;
        }

        string? reason = _dialogService.ShowReasonPromptDialog(
            "Cancel Sale",
            $"Are you sure you want to cancel sale '{SelectedItem.SaleNumber}'? Inventory will be restored.",
            "Customer Cancellation");

        if (string.IsNullOrWhiteSpace(reason)) return;

        var result = await _saleService.CancelSaleAsync(SelectedItem.Id, reason);
        if (result.Success)
        {
            _dialogService.ShowMessage($"Sale '{SelectedItem.SaleNumber}' was cancelled successfully and stock was restored.", "Sale Cancelled");
            await LoadSalesAsync();
        }
        else
        {
            _dialogService.ShowMessage(result.Error ?? "Could not cancel sale.", "Error");
        }
    }

    [RelayCommand]
    public void StartReturn()
    {
        if (SelectedItem == null)
        {
            _dialogService.ShowMessage("Please select a sale to initiate a return.", "Start Return");
            return;
        }

        var returnVm = new SaleReturnDialogViewModel(
            SelectedItem.Id,
            SelectedItem.SaleNumber,
            _saleReturnService,
            _saleService,
            _shopSettingService,
            _dialogService);

        bool? posted = _dialogService.ShowSaleReturnDialog(returnVm);
        if (posted == true && returnVm.Confirmed)
        {
            _ = LoadSalesAsync();
        }
    }

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        if (Items.Count == 0)
        {
            _dialogService.ShowMessage("No sales to export with current filters.", "Export CSV");
            return;
        }

        string? filePath = _dialogService.ShowSaveFileDialog("CSV Files (*.csv)|*.csv", $"Sales_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            byte[] bytes = await _salesExportService.ExportSalesToCsvAsync(Items.ToList());
            await File.WriteAllBytesAsync(filePath, bytes);
            _dialogService.ShowMessage($"Exported {Items.Count} sales to '{filePath}'.", "Export Complete");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Export failed: {ex.Message}", "Export Error");
        }
    }

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        if (Items.Count == 0)
        {
            _dialogService.ShowMessage("No sales to export with current filters.", "Export PDF");
            return;
        }

        string? filePath = _dialogService.ShowSaveFileDialog("PDF Files (*.pdf)|*.pdf", $"SalesSummary_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            var settingDto = new ShopSettingDto
            {
                ShopName = settings.ShopName,
                Address = settings.Address,
                Phone = settings.Phone,
                TaxNumber = settings.TaxNumber,
                CurrencySymbol = settings.CurrencySymbol
            };

            byte[] bytes = await _pdfReportService.GenerateSalesListPdfAsync(Items.ToList(), settingDto);
            await File.WriteAllBytesAsync(filePath, bytes);
            _dialogService.ShowMessage($"Generated sales summary report at '{filePath}'.", "Export Complete");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Export failed: {ex.Message}", "Export Error");
        }
    }

    [RelayCommand]
    public async Task NextPageAsync()
    {
        if (PageNumber < TotalPages)
        {
            PageNumber++;
            await LoadSalesAsync();
        }
    }

    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (PageNumber > 1)
        {
            PageNumber--;
            await LoadSalesAsync();
        }
    }
}
