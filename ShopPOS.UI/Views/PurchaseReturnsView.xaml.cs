using System.Windows;
using System.Windows.Controls;
using ShopPOS.UI.ViewModels.PurchaseReturns;

namespace ShopPOS.UI.Views;

public partial class PurchaseReturnsView : UserControl
{
    public PurchaseReturnsView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PurchaseReturnsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}

