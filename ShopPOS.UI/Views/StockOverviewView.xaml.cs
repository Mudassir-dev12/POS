using System.Windows;
using System.Windows.Controls;
using ShopPOS.UI.ViewModels.Inventory;

namespace ShopPOS.UI.Views;

public partial class StockOverviewView : UserControl
{
    public StockOverviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateColumnVisibility(e.NewValue as StockOverviewViewModel);
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StockOverviewViewModel vm)
        {
            UpdateColumnVisibility(vm);
            await vm.InitializeAsync();
        }
    }

    private void UpdateColumnVisibility(StockOverviewViewModel? vm)
    {
        if (vm == null) return;
        var vis = vm.IsCashier ? Visibility.Collapsed : Visibility.Visible;
        if (CostPriceColumn != null) CostPriceColumn.Visibility = vis;
        if (ValueAtCostColumn != null) ValueAtCostColumn.Visibility = vis;
    }
}

