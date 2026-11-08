using System.Windows;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.Views.Dialogs;

public partial class BackupPasswordDialog : Window
{
    public BackupPasswordDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is BackupPasswordDialogViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(BackupPasswordDialogViewModel.DialogResult) && vm.DialogResult.HasValue)
                {
                    DialogResult = vm.DialogResult.Value;
                    Close();
                }
            };
        }
    }

    private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupPasswordDialogViewModel vm)
        {
            vm.CurrentPassword = CurrentPasswordBox.Password;
        }
    }

    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupPasswordDialogViewModel vm)
        {
            vm.NewPassword = NewPasswordBox.Password;
        }
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupPasswordDialogViewModel vm)
        {
            vm.ConfirmPassword = ConfirmPasswordBox.Password;
        }
    }
}

