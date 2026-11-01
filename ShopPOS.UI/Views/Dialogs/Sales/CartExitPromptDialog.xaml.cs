using System.Windows;
using ShopPOS.UI.ViewModels.Sales;

namespace ShopPOS.UI.Views.Dialogs.Sales;

public partial class CartExitPromptDialog : Window
{
    public CartExitPromptDialog()
    {
        InitializeComponent();
    }

    public CartExitPromptDialog(CartExitPromptDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () => Close();
    }
}
