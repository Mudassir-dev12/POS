namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Status summary for the Backup and Restore header overview cards.
/// </summary>
public class BackupSummaryDto
{
    public DateTime? LastBackupAt { get; set; }
    public string? LastBackupAtFormatted => LastBackupAt?.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
    public string LastBackupResult { get; set; } = "None";
    public bool LastBackupSucceeded { get; set; } = true;
    public string? LastBackupFileName { get; set; }

    public DateTime? NextScheduledBackupAt { get; set; }
    public string NextScheduledBackupFormatted => NextScheduledBackupAt.HasValue
        ? NextScheduledBackupAt.Value.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt")
        : "Disabled";

    public string BackupDirectory { get; set; } = string.Empty;
    public long FreeDiskSpaceBytes { get; set; }
    public string FreeDiskSpaceFormatted => FormatBytes(FreeDiskSpaceBytes);

    public string DriveStatusText { get; set; } = "Not connected yet";

    // Phase 7B Drive Summary Extensions
    public bool IsDriveConnected { get; set; }
    public string? DriveAccountEmail { get; set; }
    public int DrivePendingCount { get; set; }
    public bool IsWaitingForInternet { get; set; }
    public bool IsDriveReconnectNeeded { get; set; }
    public bool IsDriveFull { get; set; }
    public bool IsDriveConfiguredInBuild { get; set; }
    public string? DriveFolderUrl { get; set; }
    public DateTime? LastDriveUploadAt { get; set; }
    public string? LastDriveUploadFormatted => LastDriveUploadAt?.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");

    // Phase 7B-GitHub Summary Extensions
    public bool IsGitHubConnected { get; set; }
    public string? GitHubRepoFullName { get; set; }
    public int GitHubPendingCount { get; set; }
    public bool IsGitHubReconnectNeeded { get; set; }
    public bool IsGitHubRepoPublic { get; set; }
    public bool IsGitHubFileTooLarge { get; set; }
    public DateTime? LastGitHubUploadAt { get; set; }
    public string? LastGitHubUploadFormatted => LastGitHubUploadAt?.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");

    public bool HasWarning { get; set; }
    public string? WarningMessage { get; set; }

    public int TotalBackupsCount { get; set; }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):F1} MB";
        return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
    }
}

