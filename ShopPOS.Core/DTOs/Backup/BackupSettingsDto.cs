namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Data transfer object for managing backup schedule and retention settings.
/// </summary>
public class BackupSettingsDto
{
    public bool BackupEnabled { get; set; } = true;
    public string BackupTime { get; set; } = "22:00";
    public bool BackupOnAppClose { get; set; } = true;
    public int KeepDaily { get; set; } = 7;
    public int KeepWeekly { get; set; } = 4;
    public int KeepMonthly { get; set; } = 6;
    public string? SecondFolderPath { get; set; }
    public bool EncryptBackups { get; set; }
    public bool HasPasswordSet { get; set; }
    public DateTime? LastSuccessfulBackupAt { get; set; }
    public string? LastBackupError { get; set; }

    // Phase 7B Google Drive Settings
    public bool GoogleDriveEnabled { get; set; }
    public string? GoogleDriveAccountEmail { get; set; }
    public string? GoogleDriveFolderId { get; set; }
    public string? GoogleDriveSubFolderId { get; set; }
    public bool KeepManualBackupsOnDrive { get; set; } = true;
    public bool GoogleDriveDeleteToTrash { get; set; }
    public DateTime? LastSuccessfulDriveUploadAt { get; set; }
    public string? LastDriveUploadError { get; set; }
    public bool GoogleDriveReconnectNeeded { get; set; }
    public bool GoogleDriveIsFull { get; set; }
    public bool IsDriveConfiguredInBuild { get; set; }
    public bool IsDriveConnected => !string.IsNullOrWhiteSpace(GoogleDriveAccountEmail);

    // Phase 7B-GitHub Private Repository Settings
    public bool GitHubEnabled { get; set; }
    public string? GitHubOwner { get; set; }
    public string? GitHubRepo { get; set; }
    public string GitHubBranch { get; set; } = "main";
    public bool GitHubExcludeImages { get; set; } = true;
    public DateTime? LastSuccessfulGitHubUploadAt { get; set; }
    public string? LastGitHubUploadError { get; set; }
    public bool GitHubReconnectNeeded { get; set; }
    public bool GitHubRepoIsPublic { get; set; }
    public bool GitHubFileTooLarge { get; set; }
    public bool HasGitHubToken { get; set; }
    public bool IsGitHubConnected => GitHubEnabled && !string.IsNullOrWhiteSpace(GitHubOwner) && !string.IsNullOrWhiteSpace(GitHubRepo) && HasGitHubToken;
}

