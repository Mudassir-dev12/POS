using System.Windows;
using ShopPOS.UI.ViewModels.Products;

namespace ShopPOS.UI.Views.Dialogs;

public partial class CsvImportWizardDialog : Window
{
    public CsvImportWizardDialog(CsvImportWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += completed =>
        {
            DialogResult = completed;
            Close();
        };
    }
}

