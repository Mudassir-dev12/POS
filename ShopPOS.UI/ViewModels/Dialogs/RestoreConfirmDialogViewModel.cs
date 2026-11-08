using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Dialogs;

public record RestoreComparisonItem(string EntityName, int BackupCount, int CurrentCount);

/// <summary>
/// ViewModel driving the pre-restore confirmation dialog with schema check, row comparison, and RESTORE confirmation word.
/// </summary>
public partial class RestoreConfirmDialogViewModel : ObservableObject
{
    private readonly IRestoreService _restoreService;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _createdAtFormatted = string.Empty;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private string _schemaVersion = string.Empty;

    [ObservableProperty]
    private string _shopName = string.Empty;

    [ObservableProperty]
    private bool _isEncrypted;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _passwordError;

    [ObservableProperty]
    private bool _passwordValidated;

    [ObservableProperty]
    private string _typedConfirmation = string.Empty;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool? _dialogResult;

    public ObservableCollection<RestoreComparisonItem> ComparisonItems { get; } = new();

    public bool CanRestore =>
        string.Equals(TypedConfirmation?.Trim(), "RESTORE", StringComparison.Ordinal) &&
        (!IsEncrypted || PasswordValidated);

    public RestoreConfirmDialogViewModel(IRestoreService restoreService)
    {
        _restoreService = restoreService;
    }

    public async Task InitializeWithValidationAsync(BackupValidationResultDto validation)
    {
        FilePath = validation.FilePath;
        FileName = Path.GetFileName(validation.FilePath);
        IsEncrypted = validation.IsEncrypted;

        if (validation.Manifest != null)
        {
            CreatedAtFormatted = validation.Manifest.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
            AppVersion = validation.Manifest.AppVersion;
            SchemaVersion = validation.Manifest.SchemaVersion;
            ShopName = validation.Manifest.ShopName;
            PopulateComparisonItems(validation.Manifest.TableRowCounts, validation.CurrentDatabaseRowCounts);
            PasswordValidated = true;
        }
        else
        {
            PasswordValidated = false;
        }

        if (!validation.IsValid && !validation.RequiresPassword)
        {
            ErrorMessage = validation.ErrorMessage;
        }

        OnPropertyChanged(nameof(CanRestore));
    }

    partial void OnTypedConfirmationChanged(string value)
    {
        OnPropertyChanged(nameof(CanRestore));
    }

    partial void OnPasswordChanged(string value)
    {
        PasswordValidated = false;
        PasswordError = null;
        OnPropertyChanged(nameof(CanRestore));
    }

    [RelayCommand]
    public async Task ValidatePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Please enter the backup password.";
            return;
        }

        IsValidating = true;
        PasswordError = null;
        try
        {
            var result = await _restoreService.ValidateBackupForRestoreAsync(FilePath, Password);
            if (result.IsValid && result.Manifest != null)
            {
                PasswordValidated = true;
                CreatedAtFormatted = result.Manifest.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
                AppVersion = result.Manifest.AppVersion;
                SchemaVersion = result.Manifest.SchemaVersion;
                ShopName = result.Manifest.ShopName;
                PopulateComparisonItems(result.Manifest.TableRowCounts, result.CurrentDatabaseRowCounts);
            }
            else
            {
                PasswordValidated = false;
                PasswordError = result.ErrorMessage ?? "Invalid password or corrupted backup file.";
            }
        }
        catch (Exception ex)
        {
            PasswordValidated = false;
            PasswordError = ex.Message;
        }
        finally
        {
            IsValidating = false;
            OnPropertyChanged(nameof(CanRestore));
        }
    }

    private void PopulateComparisonItems(Dictionary<string, int>? backupCounts, Dictionary<string, int> currentCounts)
    {
        ComparisonItems.Clear();
        string[] entities = { "Products", "Sales", "Purchases", "Categories", "Suppliers", "Expenses", "StockAdjustments" };

        foreach (var entity in entities)
        {
            int bCount = backupCounts?.GetValueOrDefault(entity, 0) ?? 0;
            int cCount = currentCounts.GetValueOrDefault(entity, 0);
            ComparisonItems.Add(new RestoreComparisonItem(entity, bCount, cCount));
        }
    }

    [RelayCommand]
    public void Confirm()
    {
        if (!CanRestore) return;
        DialogResult = true;
    }

    [RelayCommand]
    public void Cancel()
    {
        DialogResult = false;
    }
}

