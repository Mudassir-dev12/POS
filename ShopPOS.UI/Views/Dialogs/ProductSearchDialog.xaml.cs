using System.Windows;
using System.Windows.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.UI.ViewModels.Purchases;

namespace ShopPOS.UI.Views.Dialogs;

public partial class ProductSearchDialog : Window
{
    public ProductSearchDialogViewModel ViewModel { get; }
    public ProductListItemDto? SelectedProduct { get; private set; }
    public IReadOnlyList<ProductListItemDto> SelectedProducts { get; private set; } = Array.Empty<ProductListItemDto>();

    public ProductSearchDialog(ProductSearchDialogViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewModel.RequestClose += products =>
        {
            SelectedProducts = products ?? Array.Empty<ProductListItemDto>();
            SelectedProduct = SelectedProducts.FirstOrDefault();
            DialogResult = SelectedProducts.Count > 0;
            Close();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        SearchBox.Focus();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel.ConfirmSelectionCommand.Execute(null);
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddSelectedCommand.Execute(null);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelCommand.Execute(null);
    }
}
