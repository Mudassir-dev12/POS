using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class CartExitPromptDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = "The current cart contains items. What would you like to do before closing or clearing?";

    public CartExitAction SelectedAction { get; private set; } = CartExitAction.Keep;

    public event Action? RequestClose;

    [RelayCommand]
    public void HoldBill()
    {
        SelectedAction = CartExitAction.Hold;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Discard()
    {
        SelectedAction = CartExitAction.Discard;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Keep()
    {
        SelectedAction = CartExitAction.Keep;
        RequestClose?.Invoke();
    }
}

