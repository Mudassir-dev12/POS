using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Purchases;

namespace ShopPOS.UI.Views;

public partial class PurchasesView : UserControl
{
    public PurchasesView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PurchasesViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PurchasesViewModel vm)
        {
            vm.OpenPurchaseCommand.Execute(null);
        }
    }
}

