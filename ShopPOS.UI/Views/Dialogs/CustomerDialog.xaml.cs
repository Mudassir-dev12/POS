using System.Windows;
using ShopPOS.UI.ViewModels.Customers;

namespace ShopPOS.UI.Views.Dialogs;

public partial class CustomerDialog : Window
{
    public CustomerDialog(CustomerDialogViewModel viewModel)
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

