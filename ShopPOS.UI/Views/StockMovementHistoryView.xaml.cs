using System.Windows;
using System.Windows.Controls;
using ShopPOS.UI.ViewModels.Inventory;

namespace ShopPOS.UI.Views;

public partial class StockMovementHistoryView : UserControl
{
    public StockMovementHistoryView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StockMovementHistoryViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}

