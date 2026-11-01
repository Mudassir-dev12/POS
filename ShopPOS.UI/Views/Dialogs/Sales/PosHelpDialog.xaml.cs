using System.Windows;
using ShopPOS.UI.ViewModels.Sales;

namespace ShopPOS.UI.Views.Dialogs.Sales;

public partial class PosHelpDialog : Window
{
    public PosHelpDialog()
    {
        InitializeComponent();
    }

    public PosHelpDialog(PosHelpDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () => Close();
    }
}
