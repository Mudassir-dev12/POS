using System.Windows;
using ShopPOS.UI.ViewModels.Sales;

namespace ShopPOS.UI.Views.Dialogs.Sales;

public partial class ReceiptPreviewDialog : Window
{
    public ReceiptPreviewDialog()
    {
        InitializeComponent();
    }

    public ReceiptPreviewDialog(ReceiptPreviewDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
