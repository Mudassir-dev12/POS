using System.Windows;
using System.Windows.Controls;
using ShopPOS.UI.ViewModels.BackupRestore;

namespace ShopPOS.UI.Views;

public partial class BackupRestoreView : UserControl
{
    public BackupRestoreView()
    {
        InitializeComponent();
    }

    private void OnRecordCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupRestoreViewModel vm)
        {
            vm.OnItemSelectionChanged();
        }
    }
}

