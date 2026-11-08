using System.Windows;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.Views.Dialogs;

public partial class GitHubCopyRepoDialog : Window
{
    public GitHubCopyRepoDialog()
    {
        InitializeComponent();
    }

    private void LocalFolderRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is GitHubCopyRepoDialogViewModel vm)
        {
            vm.IsFromRepoMode = false;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

