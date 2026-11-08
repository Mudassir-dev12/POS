using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Dialogs;

namespace ShopPOS.UI.ViewModels.BackupRestore;

/// <summary>
/// ViewModel for managing local database backups, retention policies, scheduled times, and restore workflows.
/// </summary>
public partial class BackupRestoreViewModel : ObservableObject
{
    private readonly IBackupService _backupService;
    private readonly IRestoreService _restoreService;
    private readonly IDialogService _dialogService;
    private readonly IAppPaths _appPaths;
    private readonly IBackupUploader? _backupUploader;
    private readonly IGitHubBackupUploader? _gitHubUploader;

    [ObservableProperty]
    private BackupSummaryDto _summary = new();

    [ObservableProperty]
    private DriveStatusSummaryDto _driveStatus = new();

    [ObservableProperty]
    private GitHubStatusSummaryDto _gitHubStatus = new();

    [ObservableProperty]
    private string _gitHubTokenInput = string.Empty;

    [ObservableProperty]
    private bool _isGitHubTesting;

    [ObservableProperty]
    private bool _isGitHubUploading;

    [ObservableProperty]
    private bool _isGitHubPreparing;

    [ObservableProperty]
    private bool _isYearEndArchiving;

    [ObservableProperty]
    private string? _gitHubWarningMessage;

    [ObservableProperty]
    private bool _hasGitHubWarning;

    [ObservableProperty]
    private bool _isDriveConnecting;

    [ObservableProperty]
    private bool _isDriveUploading;

    public bool HasUnencryptedCloudBackups =>
        Summary.IsDriveConnected && Settings.GoogleDriveEnabled && !Settings.EncryptBackups;

    [ObservableProperty]
    private BackupSettingsDto _settings = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBackingUp;

    [ObservableProperty]
    private string? _actionMessage;

    [ObservableProperty]
    private bool _hasActionSuccess;

    [ObservableProperty]
    private bool _isSelectAll;

    public ObservableCollection<BackupRecordDto> Backups { get; } = new();

    public int SelectedCount => Backups.Count(b => b.IsSelected);
    public bool HasSelection => SelectedCount > 0;

    public string SecondFolderStatusText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Settings.SecondFolderPath)) return "Not configured";
            return Directory.Exists(Settings.SecondFolderPath) ? "Reachable" : "Not reachable";
        }
    }

    public BackupRestoreViewModel(
        IBackupService backupService,
        IRestoreService restoreService,
        IDialogService dialogService,
        IAppPaths appPaths,
        IBackupUploader? backupUploader = null,
        IGitHubBackupUploader? gitHubUploader = null)
    {
        _backupService = backupService;
        _restoreService = restoreService;
        _dialogService = dialogService;
        _appPaths = appPaths;
        _backupUploader = backupUploader;
        _gitHubUploader = gitHubUploader ?? (backupUploader as IGitHubBackupUploader);

        _ = LoadDataAsync();
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            Summary = await _backupService.GetBackupSummaryAsync();
            Settings = await _backupService.GetSettingsAsync();
            if (_backupUploader != null) DriveStatus = await _backupUploader.GetStatusSummaryAsync();
            if (_gitHubUploader != null) GitHubStatus = await _gitHubUploader.GetGitHubStatusSummaryAsync();

            var records = await _backupService.GetBackupsAsync();
            Backups.Clear();
            foreach (var rec in records)
            {
                Backups.Add(rec);
            }

            OnPropertyChanged(nameof(SecondFolderStatusText));
            OnPropertyChanged(nameof(HasUnencryptedCloudBackups));
            UpdateSelectionCounts();
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Failed to load backup data: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task BackupNowAsync()
    {
        IsBackingUp = true;
        ActionMessage = "Creating backup archive...";
        HasActionSuccess = false;

        try
        {
            var result = await _backupService.CreateBackupAsync(BackupKind.Manual, password: null);

            if (result.Success)
            {
                HasActionSuccess = true;
                ActionMessage = $"Backup created successfully: {result.FileName} ({result.Record?.SizeFormatted})";
                if (!string.IsNullOrWhiteSpace(result.WarningMessage))
                {
                    ActionMessage += $" (Warning: {result.WarningMessage})";
                }
                await LoadDataAsync();
            }
            else
            {
                HasActionSuccess = false;
                ActionMessage = $"Backup failed: {result.ErrorMessage}";
                _dialogService.ShowMessage(result.ErrorMessage ?? "Backup failed.", "Backup Error");
            }
        }
        catch (Exception ex)
        {
            HasActionSuccess = false;
            ActionMessage = $"Backup error: {ex.Message}";
            _dialogService.ShowMessage(ex.Message, "Error");
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    [RelayCommand]
    public async Task RestoreFromFileAsync()
    {
        string filter = "Backup Archives (*.zip;*.enc)|*.zip;*.enc|All Files (*.*)|*.*";
        string? file = _dialogService.ShowOpenFileDialog(filter, "Select Backup Archive to Restore");
        if (string.IsNullOrWhiteSpace(file)) return;

        await InitiateRestoreWorkflowAsync(file);
    }

    [RelayCommand]
    public async Task RestoreRecordAsync(BackupRecordDto? record)
    {
        if (record == null) return;
        if (!File.Exists(record.FilePath))
        {
            _dialogService.ShowMessage($"Backup file does not exist at: {record.FilePath}", "File Missing");
            return;
        }

        await InitiateRestoreWorkflowAsync(record.FilePath);
    }

    private async Task InitiateRestoreWorkflowAsync(string filePath)
    {
        try
        {
            var validation = await _restoreService.ValidateBackupForRestoreAsync(filePath);
            if (!validation.IsValid && !validation.RequiresPassword)
            {
                _dialogService.ShowMessage(validation.ErrorMessage ?? "Backup archive is invalid.", "Cannot Restore");
                return;
            }

            var dialogVm = new RestoreConfirmDialogViewModel(_restoreService);
            await dialogVm.InitializeWithValidationAsync(validation);

            bool? confirmed = _dialogService.ShowRestoreConfirmDialog(dialogVm);
            if (confirmed != true) return;

            // Execute restore
            IsLoading = true;
            ActionMessage = "Restoring database...";

            var restoreResult = await _restoreService.RestoreBackupAsync(filePath, dialogVm.Password);

            if (restoreResult.Success)
            {
                _dialogService.ShowMessage(
                    $"{restoreResult.SummaryMessage}\n\nPre-Restore backup saved: {restoreResult.PreRestoreBackupFileName}\n\nPlease restart the application to complete reloading all data.",
                    "Restore Successful");

                await LoadDataAsync();
            }
            else
            {
                _dialogService.ShowMessage($"Restore failed: {restoreResult.ErrorMessage}", "Restore Error");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Restore error: {ex.Message}", "Restore Failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task VerifyRecordAsync(BackupRecordDto? record)
    {
        if (record == null) return;

        string? password = null;
        if (record.IsEncrypted)
        {
            var pwdVm = new BackupPasswordDialogViewModel
            {
                Title = "Enter Backup Password to Verify",
                IsChangingPassword = false
            };

            bool? res = _dialogService.ShowBackupPasswordDialog(pwdVm);
            if (res != true) return;
            password = pwdVm.NewPassword;
        }

        IsLoading = true;
        try
        {
            var verifyResult = await _backupService.VerifyBackupAsync(record.Id, password);
            if (verifyResult.IsValid)
            {
                _dialogService.ShowMessage(
                    $"Backup '{record.FileName}' passed all integrity checks.\n\nDatabase integrity: OK\nChecksum: OK\nSchema: {verifyResult.SchemaVersion ?? "Valid"}",
                    "Verification Successful");
            }
            else
            {
                _dialogService.ShowMessage($"Verification failed for '{record.FileName}':\n\n{verifyResult.Message}", "Verification Failed");
            }

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Verification error: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CopyToAsync(BackupRecordDto? record)
    {
        if (record == null) return;

        string filter = record.IsEncrypted
            ? "Encrypted Backup (*.zip.enc)|*.zip.enc|All Files (*.*)|*.*"
            : "Backup Archive (*.zip)|*.zip|All Files (*.*)|*.*";

        string? destPath = _dialogService.ShowSaveFileDialog(filter, record.FileName, "Export Backup File");
        if (string.IsNullOrWhiteSpace(destPath)) return;

        try
        {
            await _backupService.CopyBackupToAsync(record.Id, destPath);
            _dialogService.ShowMessage($"Backup saved successfully to:\n{destPath}", "Export Complete");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Failed to copy backup: {ex.Message}", "Copy Failed");
        }
    }

    [RelayCommand]
    public async Task DeleteRecordAsync(BackupRecordDto? record)
    {
        if (record == null) return;

        bool confirm = _dialogService.ShowConfirmation($"Are you sure you want to delete backup '{record.FileName}'?", "Delete Backup");
        if (!confirm) return;

        var result = await _backupService.DeleteBackupsAsync(new[] { record.Id });
        _dialogService.ShowMessage(result.Summary, "Delete Result");
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var selectedIds = Backups.Where(b => b.IsSelected).Select(b => b.Id).ToList();
        if (selectedIds.Count == 0) return;

        bool confirm = _dialogService.ShowConfirmation($"Are you sure you want to delete {selectedIds.Count} selected backup(s)?", "Delete Backups");
        if (!confirm) return;

        var result = await _backupService.DeleteBackupsAsync(selectedIds);
        _dialogService.ShowMessage(result.Summary, "Bulk Delete Result");
        await LoadDataAsync();
    }

    [RelayCommand]
    public void ToggleSelectAll()
    {
        foreach (var b in Backups)
        {
            b.IsSelected = IsSelectAll;
        }
        UpdateSelectionCounts();
    }

    public void OnItemSelectionChanged()
    {
        UpdateSelectionCounts();
    }

    private void UpdateSelectionCounts()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public void OpenBackupFolder()
    {
        try
        {
            _appPaths.EnsureDirectoriesExist();
            Process.Start(new ProcessStartInfo
            {
                FileName = _appPaths.BackupsDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Could not open folder: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public async Task DiscoverBackupsAsync()
    {
        IsLoading = true;
        try
        {
            await _backupService.RegisterExistingBackupsAsync();
            await LoadDataAsync();
            _dialogService.ShowMessage("Scanned backup folder and registered all existing backup files.", "Scan Complete");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Scan error: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void BrowseSecondFolder()
    {
        string? folder = _dialogService.ShowFolderBrowserDialog("Select Secondary Backup Mirror Folder", Settings.SecondFolderPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Settings.SecondFolderPath = folder;
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(SecondFolderStatusText));
        }
    }



    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        try
        {
            await _backupService.UpdateSettingsAsync(Settings);
            _dialogService.ShowMessage("Backup settings saved successfully.", "Settings Saved");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Failed to save settings: {ex.Message}", "Error");
        }
    }

    [ObservableProperty]
    private string _googleClientIdInput = string.Empty;

    [ObservableProperty]
    private string _googleClientSecretInput = string.Empty;

    [ObservableProperty]
    private bool _isDriveCredentialsExpanded;

    [RelayCommand]
    public void ToggleDriveCredentialsExpanded()
    {
        IsDriveCredentialsExpanded = !IsDriveCredentialsExpanded;
    }

    [RelayCommand]
    public void OpenDataFolder()
    {
        try
        {
            _appPaths.EnsureDirectoriesExist();
            Process.Start(new ProcessStartInfo
            {
                FileName = _appPaths.DataDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Could not open folder: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public async Task ImportGoogleClientJsonAsync()
    {
        string? selectedFile = _dialogService.ShowOpenFileDialog("Google Client JSON (*.json)|*.json|All Files (*.*)|*.*", "Select googledrive.client.json");
        if (string.IsNullOrWhiteSpace(selectedFile) || !File.Exists(selectedFile)) return;

        try
        {
            string content = await File.ReadAllTextAsync(selectedFile);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;
            bool isValid = root.TryGetProperty("client_id", out _) ||
                           root.TryGetProperty("installed", out _) ||
                           root.TryGetProperty("web", out _);

            if (!isValid)
            {
                _dialogService.ShowMessage("Selected JSON file does not appear to be a valid Google OAuth Client configuration.", "Invalid File Format");
                return;
            }

            _appPaths.EnsureDirectoriesExist();
            string targetPath = Path.Combine(_appPaths.DataDirectory, "googledrive.client.json");
            await File.WriteAllTextAsync(targetPath, content);

            await LoadDataAsync();
            _dialogService.ShowMessage("Google Drive credentials imported successfully to ShopData folder! You can now click 'Connect Google Drive'.", "Credentials Configured");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Failed to import Google client JSON: {ex.Message}", "Import Failed");
        }
    }

    [RelayCommand]
    public async Task SaveManualGoogleCredentialsAsync()
    {
        if (string.IsNullOrWhiteSpace(GoogleClientIdInput) || string.IsNullOrWhiteSpace(GoogleClientSecretInput))
        {
            _dialogService.ShowMessage("Please enter both Client ID and Client Secret.", "Required Fields");
            return;
        }

        try
        {
            var config = new
            {
                client_id = GoogleClientIdInput.Trim(),
                client_secret = GoogleClientSecretInput.Trim()
            };
            string json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            _appPaths.EnsureDirectoriesExist();
            string targetPath = Path.Combine(_appPaths.DataDirectory, "googledrive.client.json");
            await File.WriteAllTextAsync(targetPath, json);

            GoogleClientIdInput = string.Empty;
            GoogleClientSecretInput = string.Empty;
            IsDriveCredentialsExpanded = false;

            await LoadDataAsync();
            _dialogService.ShowMessage("Google credentials saved successfully! You can now click 'Connect Google Drive'.", "Credentials Saved");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Failed to save credentials: {ex.Message}", "Save Error");
        }
    }

    [RelayCommand]
    public async Task ClearGoogleCredentialsAsync()
    {
        bool confirm = _dialogService.ShowConfirmation("Are you sure you want to remove the Google credentials file? Google Drive sync will be disabled.", "Remove Credentials");
        if (!confirm) return;

        try
        {
            string targetPath = Path.Combine(_appPaths.DataDirectory, "googledrive.client.json");
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            if (_backupUploader != null)
            {
                await _backupUploader.DisconnectAsync();
            }
            await LoadDataAsync();
            _dialogService.ShowMessage("Google Drive credentials removed.", "Credentials Removed");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Error: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public async Task ConnectDriveAsync()
    {
        if (_backupUploader == null) return;
        if (!DriveStatus.IsConfiguredInBuild)
        {
            bool importNow = _dialogService.ShowConfirmation(
                "Google Drive credentials (googledrive.client.json) are not configured yet.\n\nWould you like to select and import your Google OAuth client JSON file now?",
                "Google Drive Configuration Required");
            if (importNow)
            {
                await ImportGoogleClientJsonAsync();
                if (!DriveStatus.IsConfiguredInBuild) return;
            }
            else
            {
                return;
            }
        }

        using var cts = new System.Threading.CancellationTokenSource();
        var progressVm = new DriveConnectProgressDialogViewModel();
        progressVm.AttachCancellationTokenSource(cts);

        var connectTask = Task.Run(async () =>
        {
            try
            {
                return await _backupUploader.ConnectAsync(null, cts.Token);
            }
            catch (Exception ex)
            {
                progressVm.ErrorMessage = ex.Message;
                return false;
            }
        });

        _ = Task.Run(async () =>
        {
            bool success = await connectTask;
            if (success)
            {
                progressVm.DialogResult = true;
            }
        });

        _dialogService.ShowDriveConnectProgressDialog(progressVm);

        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task DisconnectDriveAsync()
    {
        if (_backupUploader == null) return;
        bool confirm = _dialogService.ShowConfirmation(
            "Are you sure you want to disconnect Google Drive? Automatic cloud uploads will be stopped and saved credentials removed.",
            "Disconnect Google Drive");
        if (!confirm) return;

        IsLoading = true;
        try
        {
            await _backupUploader.DisconnectAsync();
            await LoadDataAsync();
            _dialogService.ShowMessage("Disconnected Google Drive successfully.", "Google Drive Disconnected");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Disconnect error: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task UploadNowAsync()
    {
        if (_backupUploader == null) return;
        IsDriveUploading = true;
        ActionMessage = "Starting Google Drive backup queue upload...";
        try
        {
            await _backupUploader.ProcessQueueAsync(force: true);
            await LoadDataAsync();
            ActionMessage = "Google Drive backup processing completed.";
        }
        catch (Exception ex)
        {
            ActionMessage = $"Upload error: {ex.Message}";
            _dialogService.ShowMessage($"Drive upload error: {ex.Message}", "Error");
        }
        finally
        {
            IsDriveUploading = false;
        }
    }

    [RelayCommand]
    public async Task UploadRecordToDriveAsync(BackupRecordDto? record)
    {
        if (_backupUploader == null || record == null) return;

        IsDriveUploading = true;
        ActionMessage = $"Uploading '{record.FileName}' to Google Drive...";
        try
        {
            bool success = await _backupUploader.UploadSingleBackupAsync(record.Id);
            await LoadDataAsync();
            if (success)
            {
                ActionMessage = $"Successfully uploaded '{record.FileName}' to Google Drive.";
            }
            else
            {
                ActionMessage = $"Upload failed for '{record.FileName}'.";
            }
        }
        catch (Exception ex)
        {
            ActionMessage = $"Upload error: {ex.Message}";
            _dialogService.ShowMessage($"Upload error: {ex.Message}", "Error");
        }
        finally
        {
            IsDriveUploading = false;
        }
    }

    [RelayCommand]
    public void OpenDriveFolder()
    {
        string? url = DriveStatus.FolderWebUrl ?? Summary.DriveFolderUrl;
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Could not open browser: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public async Task RestoreFromDriveAsync()
    {
        if (_backupUploader == null) return;
        var vm = new DriveBackupsDialogViewModel(_backupUploader, _dialogService, _restoreService);
        await vm.InitializeAsync();

        bool? restored = _dialogService.ShowDriveBackupsDialog(vm);
        if (restored == true)
        {
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    public async Task ToggleDriveEnabledAsync()
    {
        Settings.GoogleDriveEnabled = !Settings.GoogleDriveEnabled;
        await _backupService.UpdateSettingsAsync(Settings);
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task TestGitHubConnectionAsync()
    {
        if (_gitHubUploader == null) return;
        IsGitHubTesting = true;
        HasGitHubWarning = false;
        GitHubWarningMessage = null;

        try
        {
            var result = await _gitHubUploader.TestConnectionAsync(
                Settings.GitHubOwner ?? "",
                Settings.GitHubRepo ?? "",
                Settings.GitHubBranch ?? "main",
                string.IsNullOrWhiteSpace(GitHubTokenInput) ? null : GitHubTokenInput);

            if (result.Success)
            {
                if (!string.IsNullOrWhiteSpace(result.WarningMessage))
                {
                    HasGitHubWarning = true;
                    GitHubWarningMessage = result.WarningMessage;
                    _dialogService.ShowMessage($"Connection succeeded with warning:\n{result.WarningMessage}", "GitHub Connection Warning");
                }
                else
                {
                    _dialogService.ShowMessage("Connected successfully to private GitHub repository!", "Connection Verified");
                }
            }
            else
            {
                _dialogService.ShowMessage($"Connection test failed: {result.ErrorMessage}", "Error");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Connection test error: {ex.Message}", "Error");
        }
        finally
        {
            IsGitHubTesting = false;
        }
    }

    [RelayCommand]
    public async Task PrepareGitHubRepositoryAsync()
    {
        if (_gitHubUploader == null) return;
        IsGitHubPreparing = true;
        try
        {
            bool ok = await _gitHubUploader.PrepareRepositoryAsync();
            if (ok)
            {
                _dialogService.ShowMessage("Repository prepared successfully (initial README.md committed).", "Repository Ready");
            }
            else
            {
                _dialogService.ShowMessage("Could not prepare repository. Check permissions.", "Error");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Error: {ex.Message}", "Error");
        }
        finally
        {
            IsGitHubPreparing = false;
        }
    }

    [RelayCommand]
    public async Task SaveGitHubConfigAsync()
    {
        if (_gitHubUploader == null) return;
        try
        {
            if (Settings.GitHubEnabled && (!Settings.EncryptBackups || !Settings.HasPasswordSet))
            {
                var pwdVm = new BackupPasswordDialogViewModel
                {
                    Title = "Setup Backup Encryption Password",
                    IsChangingPassword = false
                };
                bool? pwdRes = _dialogService.ShowBackupPasswordDialog(pwdVm);
                if (pwdRes != true || string.IsNullOrWhiteSpace(pwdVm.NewPassword))
                {
                    _dialogService.ShowMessage("GitHub backup cannot be enabled without setting an encryption password.", "Encryption Required");
                    Settings.GitHubEnabled = false;
                    return;
                }
                await _backupService.SetEncryptionPasswordAsync(pwdVm.NewPassword);
                Settings.EncryptBackups = true;
                Settings.HasPasswordSet = true;
            }

            var config = GitHubConfigDto.Create(
                Settings.GitHubOwner ?? "",
                Settings.GitHubRepo ?? "",
                Settings.GitHubBranch ?? "main",
                Settings.GitHubExcludeImages,
                Settings.GitHubEnabled);
            config.Token = string.IsNullOrWhiteSpace(GitHubTokenInput) ? null : GitHubTokenInput;

            await _gitHubUploader.ConfigureAsync(config);
            await _backupService.UpdateSettingsAsync(Settings);
            GitHubTokenInput = string.Empty;
            _dialogService.ShowMessage("GitHub backup configuration saved.", "Saved");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Failed to save GitHub configuration: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public async Task DisconnectGitHubAsync()
    {
        if (_gitHubUploader == null) return;
        bool confirm = _dialogService.ShowConfirmation("Are you sure you want to disconnect GitHub and remove stored access credentials?", "Disconnect GitHub");
        if (!confirm) return;

        try
        {
            await _gitHubUploader.DisconnectAsync();
            Settings.GitHubEnabled = false;
            GitHubTokenInput = string.Empty;
            await LoadDataAsync();
            _dialogService.ShowMessage("GitHub disconnected and credentials cleared.", "Disconnected");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Error disconnecting: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    public async Task UploadRecordToGitHubAsync(BackupRecordDto? record)
    {
        if (record == null || _gitHubUploader == null) return;
        IsGitHubUploading = true;
        try
        {
            bool ok = await _gitHubUploader.UploadSingleBackupAsync(record.Id);
            await LoadDataAsync();
            if (ok)
            {
                ActionMessage = $"Successfully uploaded '{record.FileName}' to GitHub releases.";
            }
            else
            {
                ActionMessage = $"GitHub upload failed for '{record.FileName}'.";
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Upload error: {ex.Message}", "Error");
        }
        finally
        {
            IsGitHubUploading = false;
        }
    }

    [RelayCommand]
    public async Task UploadAllPendingToGitHubAsync()
    {
        if (_gitHubUploader == null) return;
        IsGitHubUploading = true;
        try
        {
            await _gitHubUploader.ProcessQueueAsync(force: true);
            await LoadDataAsync();
            _dialogService.ShowMessage("GitHub upload queue processing triggered.", "Queue Triggered");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Upload error: {ex.Message}", "Error");
        }
        finally
        {
            IsGitHubUploading = false;
        }
    }

    [RelayCommand]
    public async Task CreateYearEndArchiveAsync()
    {
        if (_gitHubUploader == null) return;
        IsYearEndArchiving = true;
        try
        {
            bool ok = await _gitHubUploader.CreateYearEndArchiveAsync();
            if (ok)
            {
                _dialogService.ShowMessage("Year-End backup archive created and uploaded successfully.", "Year-End Archive Complete");
                await LoadDataAsync();
            }
            else
            {
                _dialogService.ShowMessage("Failed to create Year-End archive.", "Error");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Error creating Year-End archive: {ex.Message}", "Error");
        }
        finally
        {
            IsYearEndArchiving = false;
        }
    }

    [RelayCommand]
    public async Task RestoreFromGitHubAsync()
    {
        if (_gitHubUploader == null) return;
        var vm = new GitHubRestoreDialogViewModel(_gitHubUploader, _dialogService, _restoreService);
        await vm.InitializeAsync();

        bool? restored = _dialogService.ShowGitHubRestoreDialog(vm);
        if (restored == true)
        {
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    public void OpenCopyRepoDialog()
    {
        if (_gitHubUploader == null) return;
        var vm = new GitHubCopyRepoDialogViewModel(_gitHubUploader, _dialogService);
        if (Settings != null)
        {
            vm.SetSourceAccount(Settings.GitHubOwner ?? "", Settings.GitHubRepo ?? "");
        }
        _dialogService.ShowGitHubCopyRepoDialog(vm);
    }
}
