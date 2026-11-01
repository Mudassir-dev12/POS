using System.Windows;
using ShopPOS.UI.ViewModels.HeldBills;

namespace ShopPOS.UI.Views.Dialogs.Sales;

public partial class HeldBillsDialog : Window
{
    public HeldBillsDialog()
    {
        InitializeComponent();
    }

    public HeldBillsDialog(HeldBillsDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () => Close();
    }
}
