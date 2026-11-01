using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Products;

namespace ShopPOS.UI.Views;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProductsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProductsViewModel vm && vm.CanEdit && vm.SelectedItem != null)
        {
            _ = vm.EditAsync();
        }
    }
}

