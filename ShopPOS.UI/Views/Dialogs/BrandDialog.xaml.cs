using System.Windows;
using ShopPOS.UI.ViewModels.Brands;

namespace ShopPOS.UI.Views.Dialogs;

public partial class BrandDialog : Window
{
    public BrandDialog(BrandDialogViewModel viewModel)
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

