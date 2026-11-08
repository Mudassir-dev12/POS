using ShopPOS.Core.DTOs.Backup;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// High-level uploader-agnostic contract for backing up and restoring data to cloud storage.
/// </summary>
public interface IBackupUploader
{
    /// <summary>
    /// Connects to Google Drive using interactive OAuth authorization.
    /// </summary>
    Task<bool> ConnectAsync(Func<string, Task>? onBrowserOpen = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from Google Drive, revokes the token, and clears stored credentials.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current connection and synchronization summary.
    /// </summary>
    Task<DriveStatusSummaryDto> GetStatusSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes all pending or retry-eligible backup uploads in the background queue.
    /// </summary>
    Task ProcessQueueAsync(bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers an immediate upload of a specific backup record.
    /// </summary>
    Task<bool> UploadSingleBackupAsync(int backupRecordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all backup files stored in the shop's Google Drive folder.
    /// </summary>
    Task<IReadOnlyList<DriveBackupItemDto>> ListDriveBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a backup archive from Drive into the local Backups folder and registers it as a BackupRecord.
    /// </summary>
    Task<string> DownloadBackupFromDriveAsync(string driveFileId, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks real HTTPS internet connectivity to Google servers.
    /// </summary>
    Task<bool> CheckInternetAccessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether a background upload operation is actively running.
    /// </summary>
    bool IsQueueBusy { get; }
}

