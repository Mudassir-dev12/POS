using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class ReturnLineItemViewModel : ObservableObject
{
    public ReturnableLineDto Line { get; }

    public int SaleItemId => Line.SaleItemId;
    public int ProductId => Line.ProductId;
    public string ProductName => Line.ProductName;
    public string Sku => Line.Sku;
    public decimal SoldQuantity => Line.SoldQuantity;
    public decimal AlreadyReturnedQuantity => Line.AlreadyReturnedQuantity;
    public decimal AvailableReturnQuantity => Line.AvailableReturnQuantity;
    public decimal UnitPrice => Line.UnitPrice;
    public decimal EffectiveUnitPrice => Line.EffectiveUnitPrice;
    public decimal TaxPercent => Line.TaxPercent;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private decimal _returnQuantity = 1m;

    [ObservableProperty]
    private bool _returnToStock = true;

    [ObservableProperty]
    private string _reason = "Customer Return";

    public decimal EstimatedLineRefund => Math.Round(ReturnQuantity * EffectiveUnitPrice, 2, MidpointRounding.AwayFromZero);

    public event Action? OnChanged;

    public ReturnLineItemViewModel(ReturnableLineDto line)
    {
        Line = line;
        _returnQuantity = Math.Min(1m, line.AvailableReturnQuantity);
    }

    partial void OnIsSelectedChanged(bool value) => OnChanged?.Invoke();

    partial void OnReturnQuantityChanged(decimal value)
    {
        if (value > AvailableReturnQuantity)
        {
            ReturnQuantity = AvailableReturnQuantity;
        }
        else if (value < 0.001m && AvailableReturnQuantity > 0m)
        {
            ReturnQuantity = 1m;
        }
        OnPropertyChanged(nameof(EstimatedLineRefund));
        OnChanged?.Invoke();
    }

    partial void OnReturnToStockChanged(bool value) => OnChanged?.Invoke();
}

public partial class SaleReturnDialogViewModel : ObservableObject
{
    private readonly ISaleReturnService _saleReturnService;
    private readonly ISaleService _saleService;
    private readonly IShopSettingService _shopSettingService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _searchInvoiceNumber = string.Empty;

    [ObservableProperty]
    private int? _loadedSaleId;

    [ObservableProperty]
    private string? _loadedSaleNumber;

    [ObservableProperty]
    private DateTime? _loadedSaleDate;

    [ObservableProperty]
    private string? _loadedCustomerName;

    [ObservableProperty]
    private int? _loadedCustomerId;

    [ObservableProperty]
    private decimal _totalRefundAmount;

    [ObservableProperty]
    private PaymentMethod _refundMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private string _globalReason = "Customer Return";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _currencySymbol = "$";

    [ObservableProperty]
    private bool _allowReturnWithoutReceipt;

    public ObservableCollection<ReturnLineItemViewModel> ReturnLines { get; } = new();

    public bool Confirmed { get; set; }
    public SaleReturnDetailDto? CreatedReturn { get; private set; }

    public event Action? RequestClose;

    public SaleReturnDialogViewModel(
        int? preloadedSaleId,
        string? preloadedSaleNumber,
        ISaleReturnService saleReturnService,
        ISaleService saleService,
        IShopSettingService shopSettingService,
        IDialogService dialogService)
    {
        _saleReturnService = saleReturnService;
        _saleService = saleService;
        _shopSettingService = shopSettingService;
        _dialogService = dialogService;

        if (preloadedSaleId.HasValue && !string.IsNullOrWhiteSpace(preloadedSaleNumber))
        {
            SearchInvoiceNumber = preloadedSaleNumber;
            _ = LoadSaleByIdAsync(preloadedSaleId.Value);
        }
        else
        {
            _ = LoadSettingsAsync();
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            CurrencySymbol = settings.CurrencySymbol;
            AllowReturnWithoutReceipt = settings.AllowReturnWithoutReceipt;
        }
        catch { }
    }

    [RelayCommand]
    public async Task SearchSaleAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(SearchInvoiceNumber))
        {
            ErrorMessage = "Please enter an invoice number to search.";
            return;
        }

        IsLoading = true;
        try
        {
            await LoadSettingsAsync();
            var searchDto = new SaleSearchDto
            {
                SearchTerm = SearchInvoiceNumber.Trim(),
                PageSize = 5
            };

            var searchResult = await _saleService.SearchSalesAsync(searchDto);
            var match = searchResult.Items.FirstOrDefault(s =>
                string.Equals(s.SaleNumber, SearchInvoiceNumber.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                ErrorMessage = $"Sale with invoice number '{SearchInvoiceNumber.Trim()}' was not found.";
                return;
            }

            await LoadSaleByIdAsync(match.Id);
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

    public async Task LoadSaleByIdAsync(int saleId)
    {
        IsLoading = true;
        ErrorMessage = null;
        ReturnLines.Clear();

        try
        {
            await LoadSettingsAsync();
            var saleResult = await _saleService.GetSaleByIdAsync(saleId);
            if (!saleResult.Success || saleResult.Data == null)
            {
                ErrorMessage = saleResult.Error ?? "Could not load sale details.";
                return;
            }

            var sale = saleResult.Data;
            LoadedSaleId = sale.Id;
            LoadedSaleNumber = sale.SaleNumber;
            LoadedSaleDate = sale.SaleDate;
            LoadedCustomerId = sale.CustomerId;
            LoadedCustomerName = sale.CustomerName ?? "Walk-in";

            var linesResult = await _saleReturnService.LoadReturnableLinesAsync(saleId);
            if (!linesResult.Success || linesResult.Data == null || linesResult.Data.Count == 0)
            {
                ErrorMessage = "No returnable items remain on this sale (all items already returned).";
                return;
            }

            foreach (var l in linesResult.Data)
            {
                var lineVm = new ReturnLineItemViewModel(l);
                lineVm.OnChanged += RecalculateRefundTotal;
                ReturnLines.Add(lineVm);
            }

            RecalculateRefundTotal();
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

    private void RecalculateRefundTotal()
    {
        decimal total = 0m;
        foreach (var l in ReturnLines.Where(x => x.IsSelected))
        {
            total += l.EstimatedLineRefund;
        }
        TotalRefundAmount = Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    [RelayCommand]
    public async Task PostReturnAsync()
    {
        ErrorMessage = null;
        var selected = ReturnLines.Where(l => l.IsSelected && l.ReturnQuantity > 0m).ToList();
        if (selected.Count == 0)
        {
            ErrorMessage = "Please select at least one item and quantity to return.";
            return;
        }

        if (RefundMethod == PaymentMethod.StoreCredit && !LoadedCustomerId.HasValue)
        {
            ErrorMessage = "Store Credit refunds can only be issued to registered customers.";
            return;
        }

        bool confirm = _dialogService.ShowConfirmation(
            $"Confirm return of {selected.Count} line(s) for a total refund of {CurrencySymbol}{TotalRefundAmount:F2} via {RefundMethod}?",
            "Confirm Sale Return");

        if (!confirm) return;

        IsLoading = true;
        try
        {
            var dto = new CreateSaleReturnDto
            {
                SaleId = LoadedSaleId,
                CustomerId = LoadedCustomerId,
                RefundMethod = RefundMethod,
                Reason = string.IsNullOrWhiteSpace(GlobalReason) ? "Customer Return" : GlobalReason.Trim(),
                Items = selected.Select(s => new CreateSaleReturnItemDto
                {
                    SaleItemId = s.SaleItemId,
                    ProductId = s.ProductId,
                    Quantity = s.ReturnQuantity,
                    ReturnToStock = s.ReturnToStock,
                    Reason = string.IsNullOrWhiteSpace(s.Reason) ? GlobalReason : s.Reason
                }).ToList()
            };

            var result = await _saleReturnService.CreateSaleReturnAsync(dto);
            if (result.Success && result.Data != null)
            {
                CreatedReturn = result.Data;
                Confirmed = true;
                _dialogService.ShowMessage($"Sale return '{result.Data.ReturnNumber}' posted successfully.", "Return Posted");
                RequestClose?.Invoke();
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to post sale return.";
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
    public void Cancel()
    {
        Confirmed = false;
        RequestClose?.Invoke();
    }
}

