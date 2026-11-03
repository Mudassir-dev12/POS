using System.Windows;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.Views.Dialogs;

public partial class ConfirmActionDialog : Window
{
    public ConfirmActionResult Result { get; private set; } = ConfirmActionResult.Cancel;

    public ConfirmActionDialog(string title, string message, bool isReferenced)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;

        if (isReferenced)
        {
            // Cannot delete referenced record, only deactivate or cancel
            DeleteButton.Visibility = Visibility.Collapsed;
            DeactivateButton.Content = "Deactivate";
            DeactivateButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(217, 119, 6)); // amber
            IconTextBlock.Text = "";
        }
        else
        {
            DeleteButton.Visibility = Visibility.Visible;
            DeactivateButton.Visibility = Visibility.Visible;
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        Result = ConfirmActionResult.Delete;
        DialogResult = true;
    }

    private void DeactivateButton_Click(object sender, RoutedEventArgs e)
    {
        Result = ConfirmActionResult.Deactivate;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = ConfirmActionResult.Cancel;
        DialogResult = false;
    }
}

