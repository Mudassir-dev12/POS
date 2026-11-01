using System.Windows;
using System.Windows.Controls;
using ShopPOS.UI.ViewModels.SupplierLedger;

namespace ShopPOS.UI.Views;

public partial class SupplierLedgerView : UserControl
{
    public SupplierLedgerView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SupplierLedgerViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}

