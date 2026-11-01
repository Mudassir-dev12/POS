using System.Windows;
using ShopPOS.UI.ViewModels.Units;

namespace ShopPOS.UI.Views.Dialogs;

public partial class UnitDialog : Window
{
    public UnitDialog(UnitDialogViewModel viewModel)
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

