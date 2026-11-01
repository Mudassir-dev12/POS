using System.Windows;
using ShopPOS.UI.ViewModels.Products;

namespace ShopPOS.UI.Views.Dialogs;

public partial class PriceHistoryDialog : Window
{
    public PriceHistoryDialog(PriceHistoryDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += () => Close();
    }
}

