using System.Linq;
using System.Text;
using System.Windows;
using ShopPOS.Core.DTOs;

namespace ShopPOS.UI.Views.Dialogs;

public partial class BulkSummaryDialog : Window
{
    private readonly BulkOperationResult _result;

    public bool DeactivateRequested { get; private set; }

    public BulkSummaryDialog(string title, BulkOperationResult result)
    {
        InitializeComponent();
        _result = result;

        Title = title;
        TitleText.Text = title;
        SummaryText.Text = $"{result.SuccessCount:N0} succeeded, {result.SkippedCount:N0} skipped.";

        var skipped = result.Items.Where(i => !i.Success).ToList();
        if (skipped.Count > 0)
        {
            SkippedGrid.ItemsSource = skipped;
            SkippedGrid.Visibility = Visibility.Visible;
            AllSucceededText.Visibility = Visibility.Collapsed;
            if (result.Items.Any(i => !i.Success && i.CanDeactivateInstead))
            {
                DeactivateBtn.Visibility = Visibility.Visible;
            }
        }
        else
        {
            SkippedGrid.Visibility = Visibility.Collapsed;
            AllSucceededText.Visibility = Visibility.Visible;
        }
    }

    private void Deactivate_Click(object sender, RoutedEventArgs e)
    {
        DeactivateRequested = true;
        DialogResult = true;
        Close();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Bulk Action Summary: {_result.SuccessCount} succeeded, {_result.SkippedCount} skipped");
        sb.AppendLine("Identifier\tReason");
        foreach (var item in _result.Items.Where(i => !i.Success))
        {
            sb.AppendLine($"{item.Identifier}\t{item.Message}");
        }

        try
        {
            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Summary copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
