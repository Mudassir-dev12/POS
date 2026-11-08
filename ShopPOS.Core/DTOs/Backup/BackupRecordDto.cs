using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Data transfer object for displaying and managing backup records.
/// </summary>
public class BackupRecordDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedAtFormatted => CreatedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
    public long SizeBytes { get; set; }
    public string SizeFormatted => FormatBytes(SizeBytes);
    public string Sha256 { get; set; } = string.Empty;
    public BackupKind Kind { get; set; }
    public string KindDisplay => Kind switch
    {
        BackupKind.Scheduled => "Scheduled",
        BackupKind.Manual => "Manual",
        BackupKind.PreMigration => "Pre-Migration",
        BackupKind.PreRestore => "Pre-Restore",
        BackupKind.PreDataTool => "Pre-DataTool",
        _ => Kind.ToString()
    };
    public bool IsEncrypted { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public BackupVerifyStatus VerifyStatus { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedAtFormatted => VerifiedAt?.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
    public BackupStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DriveUploadStatus DriveStatus { get; set; }
    public string DriveStatusDisplay => DriveStatus switch
    {
        DriveUploadStatus.NotConfigured => "Not connected",
        DriveUploadStatus.Pending => "Pending",
        DriveUploadStatus.Uploading => "Uploading...",
        DriveUploadStatus.Uploaded => "Uploaded",
        DriveUploadStatus.Failed => "Failed",
        _ => "-"
    };
    public string? DriveFileId { get; set; }
    public DateTime? DriveUploadedAt { get; set; }
    public string? DriveUploadedAtFormatted => DriveUploadedAt?.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
    public int DriveAttempts { get; set; }
    public string? DriveLastError { get; set; }
    public DateTime? DriveNextAttemptAt { get; set; }
    public string? DriveUploadSessionUri { get; set; }
    public string? DriveMd5Checksum { get; set; }
    public int UploadProgressPercent { get; set; }
    public bool CanUploadToDrive => DriveStatus != DriveUploadStatus.Uploaded && !IsDeletedLocally;
    public bool CanDownloadFromDrive => !string.IsNullOrWhiteSpace(DriveFileId);

    // GitHub Cloud Sync
    public GitHubUploadStatus GitHubStatus { get; set; }
    public string GitHubStatusDisplay => GitHubStatus switch
    {
        GitHubUploadStatus.NotConfigured => "Not configured",
        GitHubUploadStatus.Pending => "Pending",
        GitHubUploadStatus.Uploading => "Uploading...",
        GitHubUploadStatus.Uploaded => "Uploaded",
        GitHubUploadStatus.Failed => "Failed",
        GitHubUploadStatus.Skipped => "Skipped",
        _ => "-"
    };
    public string? GitHubFileSha { get; set; }
    public DateTime? GitHubUploadedAt { get; set; }
    public string? GitHubUploadedAtFormatted => GitHubUploadedAt?.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
    public int GitHubAttempts { get; set; }
    public string? GitHubLastError { get; set; }
    public DateTime? GitHubNextAttemptAt { get; set; }
    public bool CanUploadToGitHub => GitHubStatus != GitHubUploadStatus.Uploaded && !IsDeletedLocally;
    public bool CanDownloadFromGitHub => !string.IsNullOrWhiteSpace(GitHubFileSha);

    public bool IsDeletedLocally { get; set; }

    public bool IsSelected { get; set; }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
        return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
    }
}

