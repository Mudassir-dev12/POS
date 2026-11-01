using System.Windows;
using System.Windows.Controls;
using ShopPOS.UI.ViewModels.SupplierPayments;

namespace ShopPOS.UI.Views;

public partial class SupplierPaymentsView : UserControl
{
    public SupplierPaymentsView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SupplierPaymentsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}

