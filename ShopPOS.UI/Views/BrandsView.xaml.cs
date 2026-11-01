using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Brands;

namespace ShopPOS.UI.Views;

public partial class BrandsView : UserControl
{
    public BrandsView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BrandsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is BrandsViewModel vm && vm.CanEdit && vm.SelectedItem != null)
        {
            _ = vm.EditAsync();
        }
    }
}

