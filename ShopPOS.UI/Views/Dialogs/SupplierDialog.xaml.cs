using System.Windows;
using ShopPOS.UI.ViewModels.Suppliers;

namespace ShopPOS.UI.Views.Dialogs;

public partial class SupplierDialog : Window
{
    public SupplierDialog(SupplierDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += res =>
        {
            DialogResult = res;
            Close();
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FirstField.Focus();
    }
}

