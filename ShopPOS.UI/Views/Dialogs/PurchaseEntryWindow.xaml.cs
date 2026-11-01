using System.Windows;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Purchases;

namespace ShopPOS.UI.Views.Dialogs;

public partial class PurchaseEntryWindow : Window
{
    public PurchaseEntryViewModel ViewModel { get; }

    public PurchaseEntryWindow(PurchaseEntryViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsReadOnly)
        {
            BarcodeScannerBox.Focus();
        }
    }

    private void BarcodeScannerBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ViewModel.AddScannedProductCommand.Execute(null);
            BarcodeScannerBox.Focus();
        }
    }

    private void LinesGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && !ViewModel.IsReadOnly)
        {
            e.Handled = true;
            ViewModel.DeleteSelectedLineCommand.Execute(null);
        }
    }

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DeleteSelectedLineCommand.Execute(null);
    }
}

