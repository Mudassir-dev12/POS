using System.Windows;
using ShopPOS.UI.ViewModels.Settings;

namespace ShopPOS.UI.Views.Dialogs.Sales;

public partial class PrinterSettingsDialog : Window
{
    public PrinterSettingsDialog()
    {
        InitializeComponent();
    }

    public PrinterSettingsDialog(PrinterSettingsDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () => DialogResult = viewModel.Confirmed;
    }
}
