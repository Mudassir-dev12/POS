using System.ComponentModel;
using System.Windows;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.Views.Dialogs;

public partial class DriveBackupsDialog : Window
{
    public DriveBackupsDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DriveBackupsDialogViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is DriveBackupsDialogViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DriveBackupsDialogViewModel.DialogResult))
        {
            if (DataContext is DriveBackupsDialogViewModel vm && vm.DialogResult.HasValue)
            {
                try
                {
                    DialogResult = vm.DialogResult;
                }
                catch
                {
                    Close();
                }
            }
        }
    }
}

