using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs.DataTools;

namespace ShopPOS.UI.ViewModels.Dialogs;

public partial class DataResetConfirmDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Confirm Action";

    [ObservableProperty]
    private string _actionName = "Clear Data";

    [ObservableProperty]
    private string _warningMessage = "This action is destructive and cannot be undone. An automatic database backup will be created immediately before proceeding.";

    [ObservableProperty]
    private string _backupLocationPreview = string.Empty;

    [ObservableProperty]
    private string _confirmationInput = string.Empty;

    public ObservableCollection<TableCountDto> TablesToDelete { get; } = new();
    public ObservableCollection<string> TablesToKeep { get; } = new();

    public bool CanConfirm => string.Equals(ConfirmationInput?.Trim(), "RESET", StringComparison.OrdinalIgnoreCase);

    public bool DialogResult { get; private set; }

    public event Action? RequestClose;

    partial void OnConfirmationInputChanged(string value)
    {
        OnPropertyChanged(nameof(CanConfirm));
    }

    [RelayCommand]
    private void Confirm()
    {
        if (!CanConfirm) return;
        DialogResult = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke();
    }
}

