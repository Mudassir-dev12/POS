using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a local or remote database backup archive record.
/// </summary>
public class BackupRecord : BaseEntity
{
    /// <summary>
    /// File name of the backup archive (e.g. ShopPOS_MyMart_20261104-120000_manual.zip).
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Full absolute path to the local backup archive file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Size of the backup archive in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// SHA-256 hash checksum of the backup archive file.
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>
    /// Origin or triggering kind of the backup.
    /// </summary>
    public BackupKind Kind { get; set; } = BackupKind.Manual;

    /// <summary>
    /// Whether the backup archive is encrypted with AES-256-GCM.
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Database schema version (latest applied EF Core migration ID) when the backup was taken.
    /// </summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Application version string when the backup was taken.
    /// </summary>
    public string AppVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Verification status of the backup archive.
    /// </summary>
    public BackupVerifyStatus VerifyStatus { get; set; } = BackupVerifyStatus.NotVerified;

    /// <summary>
    /// Timestamp when verification was last performed.
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// Overall creation status of the backup.
    /// </summary>
    public BackupStatus Status { get; set; } = BackupStatus.Created;

    /// <summary>
    /// Error message if backup creation or verification failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Cloud sync status (designed for Phase 7B Google Drive integration).
    /// </summary>
    public DriveUploadStatus DriveStatus { get; set; } = DriveUploadStatus.NotConfigured;

    /// <summary>
    /// Remote cloud file ID (null if not uploaded).
    /// </summary>
    public string? DriveFileId { get; set; }

    /// <summary>
    /// Timestamp when uploaded to cloud storage.
    /// </summary>
    public DateTime? DriveUploadedAt { get; set; }

    /// <summary>
    /// Number of cloud upload attempts.
    /// </summary>
    public int DriveAttempts { get; set; }

    /// <summary>
    /// Last cloud upload error message.
    /// </summary>
    public string? DriveLastError { get; set; }

    /// <summary>
    /// Timestamp for next retry attempt for cloud sync.
    /// </summary>
    public DateTime? DriveNextAttemptAt { get; set; }

    /// <summary>
    /// Active resumable upload session URI for resuming interrupted uploads.
    /// </summary>
    public string? DriveUploadSessionUri { get; set; }

    /// <summary>
    /// MD5 checksum returned by Google Drive or calculated locally for verification.
    /// </summary>
    public string? DriveMd5Checksum { get; set; }

    /// <summary>
    /// Cloud sync status for the private GitHub repository backup.
    /// </summary>
    public GitHubUploadStatus GitHubStatus { get; set; } = GitHubUploadStatus.NotConfigured;

    /// <summary>
    /// Git blob or commit SHA recorded upon successful upload to GitHub.
    /// </summary>
    public string? GitHubFileSha { get; set; }

    /// <summary>
    /// Timestamp when uploaded to the GitHub repository.
    /// </summary>
    public DateTime? GitHubUploadedAt { get; set; }

    /// <summary>
    /// Number of GitHub upload attempts made for this record.
    /// </summary>
    public int GitHubAttempts { get; set; }

    /// <summary>
    /// Last GitHub upload error message.
    /// </summary>
    public string? GitHubLastError { get; set; }

    /// <summary>
    /// Next retry attempt scheduled for GitHub upload.
    /// </summary>
    public DateTime? GitHubNextAttemptAt { get; set; }

    /// <summary>
    /// Indicates whether the local archive file was purged (e.g. by retention policy).
    /// </summary>
    public bool IsDeletedLocally { get; set; }
}

