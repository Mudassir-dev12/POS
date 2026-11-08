using System.Windows;
using ShopPOS.UI.ViewModels.Reports;

namespace ShopPOS.UI.Views.Dialogs;

public partial class ReportPreviewDialog : Window
{
    public ReportPreviewDialog()
    {
        InitializeComponent();
    }

    public ReportPreviewDialog(ReportPreviewDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}

