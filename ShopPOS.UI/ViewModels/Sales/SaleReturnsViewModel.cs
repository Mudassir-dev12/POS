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
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private DateTime? _dateFrom = DateTime.Today.AddDays(-30);

    [ObservableProperty]
    private DateTime? _dateTo = DateTime.Today;

    [ObservableProperty]
    private SaleReturnDetailDto? _selectedItem;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _currencySymbol = "$";

    public ObservableCollection<SaleReturnDetailDto> Items { get; } = new();

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

        await LoadReturnsAsync();
    }

    [RelayCommand]
    public async Task LoadReturnsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
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
            _ = LoadReturnsAsync();
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
            await LoadReturnsAsync();
        }
        else
        {
            _dialogService.ShowMessage(result.Error ?? "Could not cancel return.", "Error");
        }
    }
}

