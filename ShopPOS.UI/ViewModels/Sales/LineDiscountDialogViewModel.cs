using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class LineDiscountDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private decimal _unitPrice;

    [ObservableProperty]
    private decimal _quantity;

    [ObservableProperty]
    private bool _isPercentage = true;

    [ObservableProperty]
    private decimal _discountPercent;

    [ObservableProperty]
    private decimal _discountAmount;

    public bool Confirmed { get; private set; }

    public event Action? RequestClose;

    public LineDiscountDialogViewModel(string productName, decimal unitPrice, decimal quantity, decimal currentPercent, decimal currentAmount)
    {
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;

        if (currentAmount > 0m)
        {
            IsPercentage = false;
            DiscountAmount = currentAmount;
        }
        else
        {
            IsPercentage = true;
            DiscountPercent = currentPercent;
        }
    }

    [RelayCommand]
    public void Apply()
    {
        if (IsPercentage)
        {
            DiscountAmount = 0m;
            if (DiscountPercent < 0m) DiscountPercent = 0m;
            if (DiscountPercent > 100m) DiscountPercent = 100m;
        }
        else
        {
            DiscountPercent = 0m;
            if (DiscountAmount < 0m) DiscountAmount = 0m;
            decimal maxDiscount = UnitPrice * Quantity;
            if (DiscountAmount > maxDiscount) DiscountAmount = maxDiscount;
        }

        Confirmed = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        Confirmed = false;
        RequestClose?.Invoke();
    }
}

