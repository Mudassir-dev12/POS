using System.Windows;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.Views.Dialogs;

public partial class DataResetConfirmDialog : Window
{
    public DataResetConfirmDialog()
    {
        InitializeComponent();
    }

    public DataResetConfirmDialog(DataResetConfirmDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () =>
        {
            try
            {
                DialogResult = viewModel.DialogResult;
            }
            catch
            {
                Close();
            }
        };
    }
}

