using System.Windows;
using System.Windows.Controls;
using ShopPOS.UI.ViewModels.Dashboard;

namespace ShopPOS.UI.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}

