using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.Enums;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class PosCartLineItemViewModel : ObservableObject
{
    [ObservableProperty]
    private int _lineNo;

    [ObservableProperty]
    private int _productId;

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string? _barcode;

    [ObservableProperty]
    private decimal _quantity = 1m;

    [ObservableProperty]
    private decimal _unitPrice;

    [ObservableProperty]
    private decimal? _minSalePrice;

    [ObservableProperty]
    private decimal _costPriceSnapshot;

    [ObservableProperty]
    private bool _allowDecimalQuantity;

    [ObservableProperty]
    private decimal _discountPercent;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private decimal _taxPercent;

    [ObservableProperty]
    private decimal _taxAmount;

    [ObservableProperty]
    private decimal _lineTotal;

    [ObservableProperty]
    private decimal _currentStock;

    [ObservableProperty]
    private bool _trackStock = true;

    [ObservableProperty]
    private StockStatus _stockStatus = StockStatus.OK;

    [ObservableProperty]
    private string? _note;

    public event Action? OnItemChanged;

    public bool IsLowOrOutOfStock => TrackStock && (StockStatus == StockStatus.Low || StockStatus == StockStatus.OutOfStock || StockStatus == StockStatus.Negative);

    [RelayCommand]
    public void IncrementQuantity()
    {
        Quantity += 1m;
        OnItemChanged?.Invoke();
    }

    [RelayCommand]
    public void DecrementQuantity()
    {
        if (Quantity > 1m)
        {
            Quantity -= 1m;
            OnItemChanged?.Invoke();
        }
    }

    partial void OnQuantityChanged(decimal value)
    {
        if (!AllowDecimalQuantity && value % 1m != 0m)
        {
            Quantity = Math.Floor(value);
        }
        OnItemChanged?.Invoke();
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        OnItemChanged?.Invoke();
    }

    partial void OnDiscountPercentChanged(decimal value)
    {
        OnItemChanged?.Invoke();
    }

    partial void OnDiscountAmountChanged(decimal value)
    {
        OnItemChanged?.Invoke();
    }
}

