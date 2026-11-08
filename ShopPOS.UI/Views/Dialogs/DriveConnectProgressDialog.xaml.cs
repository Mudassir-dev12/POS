using System.ComponentModel;
using System.Windows;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.Views.Dialogs;

public partial class DriveConnectProgressDialog : Window
{
    public DriveConnectProgressDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DriveConnectProgressDialogViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is DriveConnectProgressDialogViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DriveConnectProgressDialogViewModel.DialogResult))
        {
            if (DataContext is DriveConnectProgressDialogViewModel vm && vm.DialogResult.HasValue)
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

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is DriveConnectProgressDialogViewModel vm)
        {
            vm.StopTimer();
        }
        base.OnClosing(e);
    }
}

