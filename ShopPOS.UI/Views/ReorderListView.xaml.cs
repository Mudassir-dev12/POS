using System.Windows;
using System.Windows.Controls;
using ShopPOS.UI.ViewModels.Inventory;

namespace ShopPOS.UI.Views;

public partial class ReorderListView : UserControl
{
    public ReorderListView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReorderListViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}

