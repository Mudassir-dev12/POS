using System.Windows;
using System.Windows.Controls;
using ShopPOS.Core.DTOs;
using ShopPOS.UI.ViewModels.Categories;

namespace ShopPOS.UI.Views;

public partial class CategoriesView : UserControl
{
    public CategoriesView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CategoriesViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is CategoriesViewModel vm && e.NewValue is CategoryDto selected)
        {
            vm.SelectedItem = selected;
        }
    }
}

