using System;
using System.Collections.ObjectModel;
using System.IO;
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
/// ViewModel for listing backups stored on GitHub Releases (daily, monthly, year-end archives) and restoring.
/// </summary>
public partial class GitHubRestoreDialogViewModel : ObservableObject
{
    private readonly IGitHubBackupUploader _gitHubUploader;
    private readonly IDialogService _dialogService;
    private readonly IRestoreService _restoreService;
    private readonly ILogger<GitHubRestoreDialogViewModel> _logger;

    [ObservableProperty]
    private string _title = "Restore Backup from GitHub";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private GitHubBackupItemDto? _selectedBackup;

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

    private readonly ObservableCollection<GitHubBackupItemDto> _allBackups = new();
    public ObservableCollection<GitHubBackupItemDto> FilteredBackups { get; } = new();

    public GitHubRestoreDialogViewModel(
        IGitHubBackupUploader gitHubUploader,
        IDialogService dialogService,
        IRestoreService restoreService,
        ILogger<GitHubRestoreDialogViewModel>? logger = null)
    {
        _gitHubUploader = gitHubUploader ?? throw new ArgumentNullException(nameof(gitHubUploader));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _restoreService = restoreService ?? throw new ArgumentNullException(nameof(restoreService));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubRestoreDialogViewModel>.Instance;
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
        StatusMessage = "Listing backup releases and assets from GitHub...";

        try
        {
            var items = await _gitHubUploader.ListGitHubBackupsAsync();
            _allBackups.Clear();
            foreach (var item in items)
            {
                _allBackups.Add(item);
            }

            ApplyFilter();
            StatusMessage = $"Found {_allBackups.Count} backup asset(s) on GitHub.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load GitHub backups");
            ErrorMessage = $"Failed to list backups: {ex.Message}";
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
            query = query.Where(b =>
                b.FileName.ToLowerInvariant().Contains(term) ||
                b.ReleaseTagName.ToLowerInvariant().Contains(term) ||
                b.KindDisplay.ToLowerInvariant().Contains(term));
        }

        foreach (var item in query)
        {
            FilteredBackups.Add(item);
        }

        if (SelectedBackup != null && !FilteredBackups.Contains(SelectedBackup))
        {
            SelectedBackup = FilteredBackups.FirstOrDefault();
        }
        else if (SelectedBackup == null && FilteredBackups.Any())
        {
            SelectedBackup = FilteredBackups.First();
        }
    }

    [RelayCommand]
    public async Task RestoreSelectedAsync()
    {
        if (SelectedBackup == null)
        {
            ErrorMessage = "Please select a backup to restore.";
            return;
        }

        IsDownloading = true;
        DownloadProgress = 0;
        ErrorMessage = null;
        StatusMessage = $"Downloading '{SelectedBackup.FileName}' from release {SelectedBackup.ReleaseTagName}...";

        try
        {
            var progressReporter = new Progress<int>(pct =>
            {
                DownloadProgress = pct;
                StatusMessage = $"Downloading {SelectedBackup.FileName}... {pct}%";
            });

            // 1. Download asset and verify SHA-256 against manifest
            string localFilePath = await _gitHubUploader.DownloadBackupFromGitHubAsync(
                SelectedBackup.AssetId.ToString(), progressReporter, CancellationToken.None);

            StatusMessage = "Downloaded and verified. Preparing database restore...";

            // 2. Validate backup archive with restore service
            var validation = await _restoreService.ValidateBackupForRestoreAsync(localFilePath);
            var restoreVm = new RestoreConfirmDialogViewModel(_restoreService);
            await restoreVm.InitializeWithValidationAsync(validation);

            // 3. Confirm restore action
            bool? confirmed = _dialogService.ShowRestoreConfirmDialog(restoreVm);
            if (confirmed != true)
            {
                StatusMessage = "Restore cancelled by user.";
                return;
            }

            StatusMessage = "Restoring database from snapshot...";

            // 4. Perform actual database restore
            var restoreResult = await _restoreService.RestoreBackupAsync(localFilePath, restoreVm.Password);
            if (restoreResult.Success)
            {
                _dialogService.ShowMessage(
                    $"Database restored successfully from GitHub backup '{SelectedBackup.FileName}'. The application will now refresh.",
                    "Restore Completed");
                DialogResult = true;
            }
            else
            {
                ErrorMessage = $"Restore failed: {restoreResult.ErrorMessage}";
                _dialogService.ShowMessage(ErrorMessage, "Restore Error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore backup from GitHub");
            ErrorMessage = $"Restore failed: {ex.Message}";
            _dialogService.ShowMessage(ErrorMessage, "Error");
        }
        finally
        {
            IsDownloading = false;
        }
    }
}

