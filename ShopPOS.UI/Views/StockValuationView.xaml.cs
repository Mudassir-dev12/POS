using System.Windows;
using System.Windows.Controls;
using ShopPOS.UI.ViewModels.Inventory;

namespace ShopPOS.UI.Views;

public partial class StockValuationView : UserControl
{
    public StockValuationView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StockValuationViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}

