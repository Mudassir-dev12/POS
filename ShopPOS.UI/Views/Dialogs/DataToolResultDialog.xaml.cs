using System.Windows;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.Views.Dialogs;

public partial class DataToolResultDialog : Window
{
    public DataToolResultDialog()
    {
        InitializeComponent();
    }

    public DataToolResultDialog(DataToolResultDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () =>
        {
            Close();
        };
    }
}

