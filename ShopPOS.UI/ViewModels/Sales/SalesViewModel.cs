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

    // Filtered range summary totals & payment breakdown
    [ObservableProperty]
    private decimal _filteredGrandTotal;

    [ObservableProperty]
    private decimal _filteredPaidAmount;

    [ObservableProperty]
    private decimal _filteredDueAmount;

    [ObservableProperty]
    private FilteredSalesPaymentBreakdownDto? _paymentBreakdown;

    [ObservableProperty]
    private SaleSummaryDto? _selectedItem;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _currencySymbol = "Rs";

    public ObservableCollection<SaleSummaryDto> Items { get; } = new();

    public SalesViewModel(
        ISaleService saleService,
        ISaleReturnService saleReturnService,
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
        await LoadSettingsAsync();
        await LoadSalesAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            CurrencySymbol = settings.CurrencySymbol;
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

            var breakdownResult = await _saleService.GetFilteredSalesPaymentBreakdownAsync(criteria);
            if (breakdownResult.Success && breakdownResult.Data != null)
            {
                PaymentBreakdown = breakdownResult.Data;
            }
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

        // Sale cancellation allowed only for completed sales dated today with no returns
        if (SelectedItem.SaleDate.Date != DateTime.Today)
        {
            _dialogService.ShowMessage($"Sale '{SelectedItem.SaleNumber}' was made on {SelectedItem.SaleDate:yyyy-MM-dd}. Sales can only be cancelled on the same day they were made (today).", "Cannot Cancel Past Sale");
            return;
        }

        string? reason = _dialogService.ShowReasonPromptDialog(
            "Cancel Sale",
            $"Are you sure you want to cancel today's sale '{SelectedItem.SaleNumber}'? Inventory will be restored.",
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

        bool? confirmed = _dialogService.ShowSaleReturnDialog(returnVm);
        if (confirmed == true && returnVm.Confirmed)
        {
            _ = LoadSalesAsync();
        }
    }

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        try
        {
            string? filePath = _dialogService.ShowSaveFileDialog("CSV Files (*.csv)|*.csv", $"Sales_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var bytes = await _salesExportService.ExportSalesToCsvAsync(Items);
            await File.WriteAllBytesAsync(filePath, bytes);
            _dialogService.ShowMessage("Sales CSV successfully exported.", "Export Complete");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Export failed: {ex.Message}", "Export Error");
        }
    }

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        try
        {
            string? filePath = _dialogService.ShowSaveFileDialog("PDF Files (*.pdf)|*.pdf", $"Sales_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var s = await _shopSettingService.GetSettingsAsync();
            var settings = new ShopSettingDto
            {
                ShopName = s?.ShopName ?? "Shop POS",
                Address = s?.Address,
                Phone = s?.Phone,
                TaxNumber = s?.TaxNumber,
                CurrencySymbol = s?.CurrencySymbol ?? "Rs"
            };
            var bytes = await _pdfReportService.GenerateSalesListPdfAsync(Items.ToList(), settings);
            await File.WriteAllBytesAsync(filePath, bytes);
            _dialogService.ShowMessage("Sales PDF report successfully exported.", "Export Complete");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Export failed: {ex.Message}", "Export Error");
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

    [RelayCommand]
    public async Task NextPageAsync()
    {
        if (PageNumber < TotalPages)
        {
            PageNumber++;
            await LoadSalesAsync();
        }
    }

    // Bulk selection
    [ObservableProperty]
    private int _selectedCount = 0;

    [ObservableProperty]
    private bool? _isAllOnPageSelected = false;

    [ObservableProperty]
    private bool _isAllResultsSelected = false;

    [ObservableProperty]
    private bool _canShowDeleteSelected = true;

    [ObservableProperty]
    private bool _canShowCancelSelected = true;

    public HashSet<int> SelectedIds { get; } = new();

    public bool HasSelection => SelectedCount > 0;

    public bool ShowSelectAllResultsBanner => TotalCount > Items.Count && IsAllOnPageSelected == true && !IsAllResultsSelected;

    public string SelectAllResultsBannerText => $"All {Items.Count} sales on this page are selected. Select all {TotalCount:N0} sales matching this filter?";

    public string SelectionStatusText => IsAllResultsSelected ? $"All {TotalCount:N0} sales selected across all pages" : $"{SelectedCount:N0} selected";

    [RelayCommand]
    public void ToggleSelectAllOnPage(bool? isChecked)
    {
        bool select = isChecked == true;
        foreach (var item in Items)
        {
            item.IsSelected = select;
            if (select) SelectedIds.Add(item.Id);
            else SelectedIds.Remove(item.Id);
        }
        if (!select) IsAllResultsSelected = false;
        UpdateSelectionState();
    }

    [RelayCommand]
    public void SelectAllOnPage()
    {
        ToggleSelectAllOnPage(IsAllOnPageSelected != true);
    }

    [RelayCommand]
    public void ItemSelectionChanged(SaleSummaryDto item)
    {
        if (item.IsSelected) SelectedIds.Add(item.Id);
        else { SelectedIds.Remove(item.Id); IsAllResultsSelected = false; }
        UpdateSelectionState();
    }

    [RelayCommand]
    public async Task SelectAllFilteredResultsAsync()
    {
        var criteria = new SaleSearchDto
        {
            SearchTerm = SearchTerm,
            Status = SelectedStatus,
            DateFrom = DateFrom,
            DateTo = DateTo
        };
        var allIds = await _saleService.GetFilteredSaleIdsAsync(criteria);
        foreach (var id in allIds) SelectedIds.Add(id);
        IsAllResultsSelected = true;
        foreach (var item in Items) item.IsSelected = true;
        UpdateSelectionState();
    }

    [RelayCommand]
    public void ClearSelection()
    {
        SelectedIds.Clear();
        IsAllResultsSelected = false;
        IsAllOnPageSelected = false;
        foreach (var item in Items) item.IsSelected = false;
        UpdateSelectionState();
    }

    public void UpdateSelectionState()
    {
        SelectedCount = SelectedIds.Count;
        int onPage = Items.Count;
        int selected = Items.Count(i => i.IsSelected);
        if (onPage == 0 || selected == 0) IsAllOnPageSelected = false;
        else if (selected == onPage) IsAllOnPageSelected = true;
        else IsAllOnPageSelected = null;

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowSelectAllResultsBanner));
        OnPropertyChanged(nameof(SelectAllResultsBannerText));
        OnPropertyChanged(nameof(SelectionStatusText));
    }

    private void SyncSelectionState()
    {
        foreach (var item in Items)
        {
            item.IsSelected = SelectedIds.Contains(item.Id);
        }
        UpdateSelectionState();
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        if (SelectedCount == 0) return;
        bool requireTyped = SelectedCount > 50 || IsAllResultsSelected;
        if (!_dialogService.ShowBulkConfirmDialog("Delete", SelectedCount, requireTyped)) return;

        var ids = SelectedIds.ToList();
        var result = await _saleService.BulkDeleteHeldBillsAsync(ids, 1);
        _dialogService.ShowBulkSummaryDialog("Bulk Delete Summary", result);
        ClearSelection();
        await LoadSalesAsync();
    }

    [RelayCommand]
    public async Task CancelSelectedAsync()
    {
        if (SelectedCount == 0) return;
        string? reason = _dialogService.ShowReasonPromptDialog("Cancel Selected", "Enter a shared cancellation reason for all selected sales:");
        if (string.IsNullOrWhiteSpace(reason)) return;

        bool requireTyped = SelectedCount > 50 || IsAllResultsSelected;
        if (!_dialogService.ShowBulkConfirmDialog("Cancel", SelectedCount, requireTyped, $"Are you sure you want to cancel {SelectedCount:N0} sales with reason: '{reason.Trim()}'?")) return;

        var ids = SelectedIds.ToList();
        var result = await _saleService.BulkCancelSalesAsync(ids, reason.Trim(), 1);
        _dialogService.ShowBulkSummaryDialog("Bulk Cancel Summary", result);
        ClearSelection();
        await LoadSalesAsync();
    }

}
