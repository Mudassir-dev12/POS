using System.Windows;
using ShopPOS.UI.ViewModels.Purchases;

namespace ShopPOS.UI.Views.Dialogs;

public partial class ReasonPromptDialog : Window
{
    public ReasonPromptDialogViewModel ViewModel { get; }
    public string? ResultReason { get; private set; }

    public ReasonPromptDialog(ReasonPromptDialogViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewModel.RequestClose += reason =>
        {
            ResultReason = reason;
            DialogResult = !string.IsNullOrWhiteSpace(reason);
            Close();
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ReasonInput.Focus();
        ReasonInput.SelectAll();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ConfirmCommand.Execute(null);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelCommand.Execute(null);
    }
}

