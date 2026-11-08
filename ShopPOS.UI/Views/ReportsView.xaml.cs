using System.Windows.Controls;
using System.Windows.Input;
using ShopPOS.UI.ViewModels.Reports;

namespace ShopPOS.UI.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
    }

    private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && DataContext is ReportsViewModel vm)
        {
            vm.DrillDown(row.Item);
        }
    }
}

