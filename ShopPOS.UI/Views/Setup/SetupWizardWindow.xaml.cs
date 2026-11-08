using System.Windows;
using ShopPOS.UI.ViewModels.Setup;

namespace ShopPOS.UI.Views.Setup;

/// <summary>
/// First-Run Setup Wizard Window shown on clean installs when no database exists.
/// </summary>
public partial class SetupWizardWindow : Window
{
    public SetupWizardViewModel ViewModel { get; }

    public SetupWizardWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SetupWizardViewModel.CompletedSuccessfully) && viewModel.CompletedSuccessfully)
            {
                Dispatcher.Invoke(() =>
                {
                    DialogResult = true;
                    Close();
                });
            }
        };

        viewModel.RequestClose += () =>
        {
            Dispatcher.Invoke(() =>
            {
                DialogResult = true;
                Close();
            });
        };
    }
}

