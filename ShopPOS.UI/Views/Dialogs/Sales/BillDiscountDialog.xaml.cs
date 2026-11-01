using System.Windows;
using ShopPOS.UI.ViewModels.Sales;

namespace ShopPOS.UI.Views.Dialogs.Sales;

public partial class BillDiscountDialog : Window
{
    public BillDiscountDialog()
    {
        InitializeComponent();
    }

    public BillDiscountDialog(BillDiscountDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () => DialogResult = viewModel.Confirmed;
    }
}
