using System;
using System.Collections.ObjectModel;
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
/// ViewModel for copying backup releases and assets to another repository or from a local folder.
/// </summary>
public partial class GitHubCopyRepoDialogViewModel : ObservableObject
{
    private readonly IGitHubBackupUploader _gitHubUploader;
    private readonly IDialogService _dialogService;
    private readonly ILogger<GitHubCopyRepoDialogViewModel> _logger;

    [ObservableProperty]
    private string _title = "Copy Backups to Another Repository";

    [ObservableProperty]
    private bool _isFromRepoMode = true;

    [ObservableProperty]
    private string _sourceAccount = string.Empty;

    [ObservableProperty]
    private string _sourceRepo = string.Empty;

    [ObservableProperty]
    private string _sourceToken = string.Empty;

    [ObservableProperty]
    private string _sourceLocalFolder = string.Empty;

    [ObservableProperty]
    private string _destAccount = string.Empty;

    [ObservableProperty]
    private string _destRepo = string.Empty;

    [ObservableProperty]
    private string _destToken = string.Empty;

    [ObservableProperty]
    private bool _isCopying;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool? _dialogResult;

    public ObservableCollection<string> LogMessages { get; } = new();

    public GitHubCopyRepoDialogViewModel(
        IGitHubBackupUploader gitHubUploader,
        IDialogService dialogService,
        ILogger<GitHubCopyRepoDialogViewModel>? logger = null)
    {
        _gitHubUploader = gitHubUploader ?? throw new ArgumentNullException(nameof(gitHubUploader));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubCopyRepoDialogViewModel>.Instance;
    }

    public void SetSourceAccount(string account, string repo)
    {
        SourceAccount = account;
        SourceRepo = repo;
    }

    [RelayCommand]
    public void BrowseLocalFolder()
    {
        string? folder = _dialogService.ShowFolderBrowserDialog("Select Local Backups Folder", SourceLocalFolder);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            SourceLocalFolder = folder;
        }
    }

    [RelayCommand]
    public async Task StartCopyAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(DestAccount) || string.IsNullOrWhiteSpace(DestRepo) || string.IsNullOrWhiteSpace(DestToken))
        {
            ErrorMessage = "Destination Owner, Repository and Personal Access Token are required.";
            return;
        }

        if (IsFromRepoMode && (string.IsNullOrWhiteSpace(SourceAccount) || string.IsNullOrWhiteSpace(SourceRepo)))
        {
            ErrorMessage = "Source Owner and Repository name are required.";
            return;
        }

        if (!IsFromRepoMode && string.IsNullOrWhiteSpace(SourceLocalFolder))
        {
            ErrorMessage = "Source local directory must be selected.";
            return;
        }

        IsCopying = true;
        ProgressPercent = 0;
        LogMessages.Clear();
        StatusMessage = "Starting migration...";

        try
        {
            var options = IsFromRepoMode
                ? GitHubCopyRepoOptionsDto.FromRepo(SourceAccount.Trim(), SourceRepo.Trim(), string.IsNullOrWhiteSpace(SourceToken) ? null : SourceToken.Trim(), DestAccount.Trim(), DestRepo.Trim(), DestToken.Trim())
                : GitHubCopyRepoOptionsDto.FromFolder(SourceLocalFolder.Trim(), DestAccount.Trim(), DestRepo.Trim(), DestToken.Trim());

            var progressReporter = new Progress<GitHubCopyProgressDto>(p =>
            {
                ProgressPercent = p.Percent;
                StatusMessage = p.Message;
                LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {p.Message}");
            });

            var result = await _gitHubUploader.CopyBackupsToAnotherRepositoryAsync(options, progressReporter, CancellationToken.None);

            foreach (var msg in result.Messages)
            {
                LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            }

            if (result.Success)
            {
                StatusMessage = $"Copy completed: {result.CopiedCount} copied, {result.SkippedCount} skipped (identical).";
                _dialogService.ShowMessage(StatusMessage, "Migration Completed");
            }
            else
            {
                ErrorMessage = "Migration completed with errors. See log for details.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy backups to destination repository");
            ErrorMessage = $"Error during copy: {ex.Message}";
        }
        finally
        {
            IsCopying = false;
        }
    }
}

