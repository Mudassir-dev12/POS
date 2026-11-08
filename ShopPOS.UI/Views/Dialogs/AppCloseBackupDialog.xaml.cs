using System.Windows;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.Views.Dialogs;

public partial class AppCloseBackupDialog : Window
{
    public AppCloseBackupDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is AppCloseBackupDialogViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(AppCloseBackupDialogViewModel.DialogResult) && vm.DialogResult.HasValue)
                {
                    DialogResult = vm.DialogResult.Value;
                    Close();
                }
            };
        }
    }
}

