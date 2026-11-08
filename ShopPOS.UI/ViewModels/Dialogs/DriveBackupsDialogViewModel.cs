using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Dialogs;

/// <summary>
/// ViewModel for listing backups stored on Google Drive and restoring from the cloud.
/// </summary>
public partial class DriveBackupsDialogViewModel : ObservableObject
{
    private readonly IBackupUploader _backupUploader;
    private readonly IDialogService _dialogService;
    private readonly IRestoreService _restoreService;
    private readonly ILogger<DriveBackupsDialogViewModel> _logger;

    [ObservableProperty]
    private string _title = "Backups on Google Drive";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DriveBackupItemDto? _selectedBackup;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private int _downloadProgress;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool? _dialogResult;

    private readonly ObservableCollection<DriveBackupItemDto> _allBackups = new();
    public ObservableCollection<DriveBackupItemDto> FilteredBackups { get; } = new();

    public DriveBackupsDialogViewModel(
        IBackupUploader backupUploader,
        IDialogService dialogService,
        IRestoreService restoreService,
        ILogger<DriveBackupsDialogViewModel>? logger = null)
    {
        _backupUploader = backupUploader;
        _dialogService = dialogService;
        _restoreService = restoreService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DriveBackupsDialogViewModel>.Instance;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadBackupsAsync();
    }

    [RelayCommand]
    public async Task LoadBackupsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Fetching backup list from Google Drive...";

        try
        {
            var items = await _backupUploader.ListDriveBackupsAsync();
            _allBackups.Clear();
            foreach (var item in items.OrderByDescending(i => i.CreatedTime ?? DateTime.MinValue))
            {
                _allBackups.Add(item);
            }

            ApplyFilter();
            StatusMessage = $"{_allBackups.Count} cloud backup(s) found.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Google Drive backups.");
            ErrorMessage = $"Failed to retrieve backups from Google Drive: {ex.Message}";
            StatusMessage = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredBackups.Clear();
        var query = _allBackups.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string term = SearchText.Trim().ToLowerInvariant();
            query = query.Where(b => b.Name.ToLowerInvariant().Contains(term));
        }

        foreach (var item in query)
        {
            FilteredBackups.Add(item);
        }

        if (SelectedBackup != null && !FilteredBackups.Contains(SelectedBackup))
        {
            SelectedBackup = FilteredBackups.FirstOrDefault();
        }
    }

    [RelayCommand]
    public async Task DownloadAndRestoreAsync()
    {
        if (SelectedBackup == null) return;

        IsDownloading = true;
        DownloadProgress = 0;
        ErrorMessage = null;
        StatusMessage = $"Downloading '{SelectedBackup.Name}' from Google Drive...";

        try
        {
            var progress = new Progress<int>(pct =>
            {
                DownloadProgress = pct;
                StatusMessage = $"Downloading '{SelectedBackup.Name}'... {pct}%";
            });

            string localPath = await _backupUploader.DownloadBackupFromDriveAsync(SelectedBackup.Id, progress, CancellationToken.None);
            StatusMessage = "Download completed. Validating archive...";

            // Open restore confirmation dialog
            var validation = await _restoreService.ValidateBackupForRestoreAsync(localPath);
            var restoreVm = new RestoreConfirmDialogViewModel(_restoreService);
            await restoreVm.InitializeWithValidationAsync(validation);

            bool? confirmed = _dialogService.ShowRestoreConfirmDialog(restoreVm);
            if (confirmed == true)
            {
                StatusMessage = "Restoring database from downloaded backup...";
                var restoreResult = await _restoreService.RestoreBackupAsync(localPath, restoreVm.Password);

                if (restoreResult.Success)
                {
                    _dialogService.ShowMessage("Database restore completed successfully! Please restart the application to complete reloading all data.", "Restore Complete");
                    DialogResult = true;
                    return;
                }
                else
                {
                    ErrorMessage = $"Restore failed: {restoreResult.ErrorMessage}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download and restore backup from Google Drive.");
            ErrorMessage = $"Download or restore failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    public void Close()
    {
        DialogResult = false;
    }
}
