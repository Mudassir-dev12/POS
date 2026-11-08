using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ShopPOS.UI.Views.Dialogs;

public partial class UnhandledExceptionDialog : Window
{
    private readonly string? _logDirectory;

    public UnhandledExceptionDialog(string message, string? stackTrace, string? logDirectory = null)
    {
        InitializeComponent();
        _logDirectory = logDirectory;

        if (!string.IsNullOrWhiteSpace(message))
        {
            MessageTextBlock.Text = message;
        }

        StackTraceTextBox.Text = stackTrace ?? "No stack trace available.";
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText($"{MessageTextBlock.Text}\n\n{StackTraceTextBox.Text}");
            CopyButton.Content = "Copied!";
        }
        catch { }
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = !string.IsNullOrWhiteSpace(_logDirectory) && Directory.Exists(_logDirectory)
                ? _logDirectory
                : AppDomain.CurrentDomain.BaseDirectory;

            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

