using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Units;

namespace ShopPOS.UI.Views;

public partial class UnitsView : UserControl
{
    public UnitsView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is UnitsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is UnitsViewModel vm && vm.CanEdit && vm.SelectedItem != null)
        {
            _ = vm.EditAsync();
        }
    }
}

