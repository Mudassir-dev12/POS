using System.Windows;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.Views.Dialogs;

public partial class RestoreConfirmDialog : Window
{
    public RestoreConfirmDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is RestoreConfirmDialogViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(RestoreConfirmDialogViewModel.DialogResult) && vm.DialogResult.HasValue)
                {
                    DialogResult = vm.DialogResult.Value;
                    Close();
                }
            };
        }
    }

    private void BackupPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is RestoreConfirmDialogViewModel vm)
        {
            vm.Password = BackupPasswordBox.Password;
        }
    }
}

