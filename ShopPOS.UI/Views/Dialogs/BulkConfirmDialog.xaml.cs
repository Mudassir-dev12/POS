using System;
using System.Windows;
using System.Windows.Controls;

namespace ShopPOS.UI.Views.Dialogs;

public partial class BulkConfirmDialog : Window
{
    private readonly string _requiredWord;
    private readonly bool _requiresTyped;

    public bool Confirmed { get; private set; }
    public bool IsConfirmEnabled => ConfirmBtn.IsEnabled;
    public void SetConfirmationInput(string text) => ConfirmationInput.Text = text;

    public BulkConfirmDialog(string actionName, int count, bool requireTypedConfirmation, string? detail = null)
    {
        InitializeComponent();
        _requiresTyped = requireTypedConfirmation;
        _requiredWord = actionName.ToUpperInvariant().Contains("CANCEL") ? "CANCEL" : "DELETE";

        Title = $"Confirm Bulk {actionName}";
        TitleText.Text = $"{actionName} {count:N0} Items";

        string verb = actionName.ToLowerInvariant();
        MessageText.Text = detail ?? $"You are about to {verb} {count:N0} selected items.\nThis action will take effect immediately.";

        if (_requiresTyped)
        {
            TypedConfirmationSection.Visibility = Visibility.Visible;
            WarningText.Text = $"Because you selected more than 50 items (or all filtered results), please type {_requiredWord} to confirm.";
            ConfirmBtn.IsEnabled = false;
        }
        else
        {
            TypedConfirmationSection.Visibility = Visibility.Collapsed;
            ConfirmBtn.IsEnabled = true;
        }

        ConfirmBtn.Content = $"Confirm {actionName}";
    }

    private void ConfirmationInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_requiresTyped) return;
        ConfirmBtn.IsEnabled = string.Equals(ConfirmationInput.Text.Trim(), _requiredWord, StringComparison.OrdinalIgnoreCase);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
        Close();
    }
}
