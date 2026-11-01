using System.Windows;
using System.Windows.Controls;
using ShopPOS.UI.ViewModels.Settings;

namespace ShopPOS.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}

