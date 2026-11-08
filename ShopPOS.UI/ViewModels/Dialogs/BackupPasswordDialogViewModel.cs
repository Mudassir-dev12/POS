using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShopPOS.UI.ViewModels.Dialogs;

/// <summary>
/// ViewModel for setting or changing the AES-256 backup encryption password.
/// </summary>
public partial class BackupPasswordDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Set Backup Password";

    [ObservableProperty]
    private bool _isChangingPassword;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private bool? _dialogResult;

    [RelayCommand]
    public void Save()
    {
        ValidationError = null;

        if (IsChangingPassword && string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ValidationError = "Please enter your current password.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            ValidationError = "Password must be at least 6 characters long.";
            return;
        }

        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            ValidationError = "Passwords do not match.";
            return;
        }

        DialogResult = true;
    }

    [RelayCommand]
    public void Cancel()
    {
        DialogResult = false;
    }
}

