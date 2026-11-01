using System.Windows;
using ShopPOS.UI.ViewModels.SupplierPayments;

namespace ShopPOS.UI.Views.Dialogs;

public partial class SupplierPaymentDialog : Window
{
    public SupplierPaymentDialogViewModel ViewModel { get; }

    public SupplierPaymentDialog(SupplierPaymentDialogViewModel viewModel)
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

