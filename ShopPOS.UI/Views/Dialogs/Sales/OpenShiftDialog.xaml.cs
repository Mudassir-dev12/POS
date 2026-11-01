using System.Windows;
using ShopPOS.UI.ViewModels.Shifts;

namespace ShopPOS.UI.Views.Dialogs.Sales;

public partial class OpenShiftDialog : Window
{
    public OpenShiftDialog()
    {
        InitializeComponent();
    }

    public OpenShiftDialog(OpenShiftDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () => DialogResult = viewModel.Confirmed;
    }
}
