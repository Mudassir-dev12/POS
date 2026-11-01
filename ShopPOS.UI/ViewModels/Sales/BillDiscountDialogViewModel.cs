using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class BillDiscountDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isPercentage = true;

    [ObservableProperty]
    private decimal _discountPercent;

    [ObservableProperty]
    private decimal _discountAmount;

    public bool Confirmed { get; private set; }

    public event Action? RequestClose;

    public BillDiscountDialogViewModel(decimal currentPercent = 0m, decimal currentAmount = 0m)
    {
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

