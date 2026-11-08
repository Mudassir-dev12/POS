using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Service contract for creating, verifying, listing, and managing database backup archives.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a consistent backup archive of the SQLite database and catalog images.
    /// Uses SQLite online backup API or VACUUM INTO to avoid locking sales in progress.
    /// </summary>
    Task<BackupResultDto> CreateBackupAsync(BackupKind kind, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the specified backup record by testing zip integrity, sha256 checksum, and SQLite integrity checks.
    /// </summary>
    Task<BackupVerifyResultDto> VerifyBackupAsync(int backupRecordId, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies an arbitrary backup file on disk.
    /// </summary>
    Task<BackupVerifyResultDto> VerifyBackupFileAsync(string filePath, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the list of all backup records.
    /// </summary>
    Task<IReadOnlyList<BackupRecordDto>> GetBackupsAsync(bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an overview summary for the backup and restore UI dashboard.
    /// </summary>
    Task<BackupSummaryDto> GetBackupSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current backup configuration settings.
    /// </summary>
    Task<BackupSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates backup configuration settings.
    /// </summary>
    Task UpdateSettingsAsync(BackupSettingsDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a new encryption password and enables backup encryption.
    /// </summary>
    Task SetEncryptionPasswordAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates current password and disables backup encryption.
    /// </summary>
    Task DisableEncryptionAsync(string currentPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the provided password matches the configured encryption key check.
    /// </summary>
    Task<bool> ValidatePasswordAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes Grandfather-Father-Son (GFS) retention rules on local backup archives.
    /// Returns the count of deleted backup files.
    /// </summary>
    Task<int> RunRetentionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific backup record and its archive file, respecting safety rules (cannot delete newest or pending upload).
    /// </summary>
    Task<bool> DeleteBackupAsync(int backupRecordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk deletes backup records, respecting safety rules (cannot delete newest or pending upload).
    /// </summary>
    Task<(int DeletedCount, int BlockedCount, string Summary)> DeleteBackupsAsync(IEnumerable<int> backupRecordIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers existing backup files on disk that are not yet tracked in the database and records them.
    /// </summary>
    Task RegisterExistingBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a backup archive to an external path (e.g. USB drive or external folder).
    /// </summary>
    Task CopyBackupToAsync(int backupRecordId, string destinationFilePath, CancellationToken cancellationToken = default);
}
