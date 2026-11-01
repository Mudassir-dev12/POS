using System.Windows;
using ShopPOS.UI.ViewModels.Sales;

namespace ShopPOS.UI.Views.Dialogs.Sales;

public partial class LineDiscountDialog : Window
{
    public LineDiscountDialog()
    {
        InitializeComponent();
    }

    public LineDiscountDialog(LineDiscountDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () => DialogResult = viewModel.Confirmed;
    }
}
