using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Sales;

namespace ShopPOS.UI.Views;

public partial class PosSaleView : UserControl
{
    public PosSaleView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ScanBox.Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PosSaleViewModel vm) return;

        if (vm.IsSaleSuccess)
        {
            if (e.Key == Key.Space || e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape)
            {
                vm.DismissSuccessPanelCommand.Execute(null);
                ScanBox.Focus();
                e.Handled = true;
                return;
            }
        }

        switch (e.Key)
        {
            case Key.F1:
                vm.ShowHelpCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F2:
                vm.OpenProductSearchCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F3:
                vm.SelectCustomerCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F4:
                vm.HoldBillCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F5:
                vm.ShowHeldBillsCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F6:
                vm.OpenBillDiscountCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F7:
                vm.OpenLineDiscountCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F8:
                vm.DiscardCartCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F9:
                vm.OpenCashDrawerCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F12:
                vm.CheckoutCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}

