namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Placeholder interface for database backup and restore operations.
/// A full implementation (including local and Google Drive sync) is scheduled for a future phase.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a local compressed backup of the SQLite database.
    /// </summary>
    /// <param name="destinationPath">Target directory or file path for the backup archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path to the generated backup file.</returns>
    Task<string> CreateLocalBackupAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a backup archive to Google Drive.
    /// </summary>
    /// <param name="localBackupFilePath">Path to the local backup file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if successfully uploaded; otherwise, <c>false</c>.</returns>
    Task<bool> UploadToCloudAsync(string localBackupFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the database from a backup file.
    /// </summary>
    /// <param name="backupFilePath">Path to the backup file to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if successfully restored; otherwise, <c>false</c>.</returns>
    Task<bool> RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default);
}

