using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Suppliers;

namespace ShopPOS.UI.Views;

public partial class SuppliersView : UserControl
{
    public SuppliersView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SuppliersViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SuppliersViewModel vm && vm.CanEdit && vm.SelectedItem != null)
        {
            _ = vm.EditAsync();
        }
    }
}

