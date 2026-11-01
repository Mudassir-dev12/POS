using System.Windows;
using ShopPOS.UI.ViewModels.PurchaseReturns;

namespace ShopPOS.UI.Views.Dialogs;

public partial class PurchaseReturnDialog : Window
{
    public PurchaseReturnDialogViewModel ViewModel { get; }

    public PurchaseReturnDialog(PurchaseReturnDialogViewModel viewModel)
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
}

