using System.Windows;
using ShopPOS.UI.ViewModels.Categories;

namespace ShopPOS.UI.Views.Dialogs;

public partial class CategoryDialog : Window
{
    public CategoryDialog(CategoryDialogViewModel viewModel)
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

