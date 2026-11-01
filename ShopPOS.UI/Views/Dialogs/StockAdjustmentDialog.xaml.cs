using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShopPOS.UI.ViewModels.Inventory;

namespace ShopPOS.UI.Views.Dialogs;

public partial class StockAdjustmentDialog : Window
{
    public StockAdjustmentDialogViewModel ViewModel => (StockAdjustmentDialogViewModel)DataContext;

    public StockAdjustmentDialog(StockAdjustmentDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }

    private void NewQuantityBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is StockAdjustmentItemLineViewModel line)
        {
            ViewModel.SelectedLine = line;
        }
    }

    private void NewQuantityBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Tab)
        {
            if (sender is TextBox currentBox && currentBox.DataContext is StockAdjustmentItemLineViewModel currentLine)
            {
                int currentIndex = ViewModel.Items.IndexOf(currentLine);
                if (currentIndex >= 0 && currentIndex + 1 < ViewModel.Items.Count)
                {
                    e.Handled = true;
                    var nextLine = ViewModel.Items[currentIndex + 1];
                    ViewModel.SelectedLine = nextLine;
                    ItemsGrid.ScrollIntoView(nextLine);

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var row = (DataGridRow)ItemsGrid.ItemContainerGenerator.ContainerFromItem(nextLine);
                        if (row != null)
                        {
                            var textBox = FindVisualChild<TextBox>(row);
                            if (textBox != null)
                            {
                                textBox.Focus();
                                textBox.SelectAll();
                            }
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }
}
