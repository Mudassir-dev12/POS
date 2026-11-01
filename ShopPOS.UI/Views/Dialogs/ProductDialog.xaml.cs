using System.Windows;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Products;

namespace ShopPOS.UI.Views.Dialogs;

public partial class ProductDialog : Window
{
    public ProductDialog(ProductDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FirstField.Focus();
    }

    private void BarcodeTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Prevent USB barcode scanner trailing ENTER from closing or submitting dialog
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
        }
    }
}

