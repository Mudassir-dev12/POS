using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Inventory;

namespace ShopPOS.UI.Views;

public partial class StockAdjustmentsView : UserControl
{
    public StockAdjustmentsView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StockAdjustmentsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is StockAdjustmentsViewModel vm && vm.OpenAdjustmentCommand.CanExecute(null))
        {
            vm.OpenAdjustmentCommand.Execute(null);
        }
    }
}

