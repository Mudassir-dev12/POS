using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class SaleReturnsViewModel : ObservableObject
{
    private readonly ISaleReturnService _saleReturnService;
    private readonly ISaleService _saleService;
    private readonly IShopSettingService _shopSettingService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private int _activeTab = 0; // 0 = Recent Purchases/Sales, 1 = Processed Returns History

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private DateTime? _dateFrom = DateTime.Today.AddDays(-30);

    [ObservableProperty]
    private DateTime? _dateTo = DateTime.Today;

    [ObservableProperty]
    private SaleReturnDetailDto? _selectedItem;

    [ObservableProperty]
    private SaleSummaryDto? _selectedSale;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _currencySymbol = "Rs";

    public ObservableCollection<SaleReturnDetailDto> Items { get; } = new();
    public ObservableCollection<SaleSummaryDto> RecentSales { get; } = new();

    public SaleReturnsViewModel(
        ISaleReturnService saleReturnService,
        ISaleService saleService,
        IShopSettingService shopSettingService,
        IDialogService dialogService)
    {
        _saleReturnService = saleReturnService;
        _saleService = saleService;
        _shopSettingService = shopSettingService;
        _dialogService = dialogService;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            CurrencySymbol = settings.CurrencySymbol;
        }
        catch { }

        await RefreshAllAsync();
    }

    [RelayCommand]
    public async Task RefreshAllAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await Task.WhenAll(LoadRecentSalesInternalAsync(), LoadReturnsInternalAsync());
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
    public async Task LoadReturnsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await LoadReturnsInternalAsync();
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

    private async Task LoadReturnsInternalAsync()
    {
        Items.Clear();

        DateTime? from = DateFrom?.Date;
        DateTime? to = DateTo?.Date.AddDays(1).AddTicks(-1);

        var criteria = new SaleReturnSearchDto
        {
            SearchTerm = SearchTerm?.Trim(),
            DateFrom = from,
            DateTo = to,
            PageNumber = 1,
            PageSize = 100
        };

        var paged = await _saleReturnService.SearchSaleReturnsAsync(criteria);
        foreach (var r in paged.Items)
        {
            Items.Add(r);
        }
    }

    [RelayCommand]
    public async Task LoadRecentSalesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await LoadRecentSalesInternalAsync();
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

    private async Task LoadRecentSalesInternalAsync()
    {
        RecentSales.Clear();

        DateTime? from = DateFrom?.Date;
        DateTime? to = DateTo?.Date.AddDays(1).AddTicks(-1);

        var criteria = new SaleSearchDto
        {
            SearchTerm = SearchTerm?.Trim(),
            DateFrom = from,
            DateTo = to,
            PageNumber = 1,
            PageSize = 100
        };

        var paged = await _saleService.SearchSalesAsync(criteria);
        foreach (var s in paged.Items)
        {
            RecentSales.Add(s);
        }
    }

    [RelayCommand]
    public void NewReturn()
    {
        var vm = new SaleReturnDialogViewModel(
            null,
            null,
            _saleReturnService,
            _saleService,
            _shopSettingService,
            _dialogService);

        bool? posted = _dialogService.ShowSaleReturnDialog(vm);
        if (posted == true && vm.Confirmed)
        {
            _ = RefreshAllAsync();
        }
    }

    [RelayCommand]
    public void ReturnSale(SaleSummaryDto? sale)
    {
        if (sale == null) return;
        var vm = new SaleReturnDialogViewModel(
            sale.Id,
            sale.SaleNumber,
            _saleReturnService,
            _saleService,
            _shopSettingService,
            _dialogService);

        bool? posted = _dialogService.ShowSaleReturnDialog(vm);
        if (posted == true && vm.Confirmed)
        {
            _ = RefreshAllAsync();
        }
    }

    [RelayCommand]
    public void ReturnSelectedSale()
    {
        if (SelectedSale != null)
        {
            ReturnSale(SelectedSale);
        }
        else
        {
            NewReturn();
        }
    }

    [RelayCommand]
    public async Task CancelReturnAsync()
    {
        if (SelectedItem == null)
        {
            _dialogService.ShowMessage("Please select a return from the list to cancel.", "Cancel Return");
            return;
        }

        string? reason = _dialogService.ShowReasonPromptDialog(
            "Cancel Return",
            $"Cancel return '{SelectedItem.ReturnNumber}'? Restocked inventory will be re-deducted.",
            "Return Mistake");

        if (string.IsNullOrWhiteSpace(reason)) return;

        var result = await _saleReturnService.CancelSaleReturnAsync(SelectedItem.Id, reason);
        if (result.Success)
        {
            _dialogService.ShowMessage($"Return '{SelectedItem.ReturnNumber}' cancelled successfully.", "Return Cancelled");
            await RefreshAllAsync();
        }
        else
        {
            _dialogService.ShowMessage(result.Error ?? "Could not cancel return.", "Error");
        }
    }

    // Bulk selection for Returns
    [ObservableProperty]
    private int _selectedCount = 0;

    [ObservableProperty]
    private bool? _isAllOnPageSelected = false;

    [ObservableProperty]
    private bool _isAllResultsSelected = false;

    [ObservableProperty]
    private bool _canShowDeleteSelected = false;

    [ObservableProperty]
    private bool _canShowCancelSelected = true;

    [ObservableProperty]
    private int _totalCount = 0;

    public HashSet<int> SelectedIds { get; } = new();

    public bool HasSelection => SelectedCount > 0;

    public bool ShowSelectAllResultsBanner => TotalCount > Items.Count && IsAllOnPageSelected == true && !IsAllResultsSelected;

    public string SelectAllResultsBannerText => $"All {Items.Count} returns on this page are selected. Select all {TotalCount:N0} returns matching this filter?";

    public string SelectionStatusText => IsAllResultsSelected ? $"All {TotalCount:N0} returns selected across all pages" : $"{SelectedCount:N0} selected";

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
    public void ItemSelectionChanged(SaleReturnDetailDto item)
    {
        if (item.IsSelected) SelectedIds.Add(item.Id);
        else { SelectedIds.Remove(item.Id); IsAllResultsSelected = false; }
        UpdateSelectionState();
    }

    [RelayCommand]
    public async Task SelectAllFilteredResultsAsync()
    {
        var criteria = new SaleReturnSearchDto
        {
            SearchTerm = SearchTerm,
            DateFrom = DateFrom,
            DateTo = DateTo
        };
        var allIds = await _saleReturnService.GetFilteredSaleReturnIdsAsync(criteria);
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
    public async Task CancelSelectedAsync()
    {
        if (SelectedCount == 0) return;
        string? reason = _dialogService.ShowReasonPromptDialog("Cancel Selected", "Enter a shared cancellation reason for all selected sale returns:");
        if (string.IsNullOrWhiteSpace(reason)) return;

        bool requireTyped = SelectedCount > 50 || IsAllResultsSelected;
        if (!_dialogService.ShowBulkConfirmDialog("Cancel", SelectedCount, requireTyped, $"Are you sure you want to cancel {SelectedCount:N0} returns with reason: '{reason.Trim()}'?")) return;

        var ids = SelectedIds.ToList();
        var result = await _saleReturnService.BulkCancelSaleReturnsAsync(ids, reason.Trim(), 1);
        _dialogService.ShowBulkSummaryDialog("Bulk Cancel Summary", result);
        ClearSelection();
        await RefreshAllAsync();
    }
}
