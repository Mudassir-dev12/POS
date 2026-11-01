using System.Windows.Controls;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Sales;

namespace ShopPOS.UI.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SalesViewModel vm && vm.OpenDetailsCommand.CanExecute(null))
        {
            vm.OpenDetailsCommand.Execute(null);
        }
    }
}

