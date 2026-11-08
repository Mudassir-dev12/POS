using System.Windows;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.Views.Dialogs;

public partial class GitHubRestoreDialog : Window
{
    public GitHubRestoreDialog()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is GitHubRestoreDialogViewModel vm)
            {
                vm.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(GitHubRestoreDialogViewModel.DialogResult) && vm.DialogResult.HasValue)
                    {
                        DialogResult = vm.DialogResult;
                        Close();
                    }
                };
                await vm.InitializeAsync();
            }
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

