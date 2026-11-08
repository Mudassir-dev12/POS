using ShopPOS.Core.DTOs.Backup;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Service contract for validating and safely executing database restore operations.
/// </summary>
public interface IRestoreService
{
    /// <summary>
    /// Inspects and validates a backup archive (.zip or .zip.enc) and returns schema/row-count comparison preview.
    /// Does not alter the database.
    /// </summary>
    Task<BackupValidationResultDto> ValidateBackupForRestoreAsync(string filePath, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a database restore from the specified backup file.
    /// Creates a PreRestore safety backup of the current database before touching anything.
    /// If successful, replaces the active SQLite database atomically and extracts catalog images.
    /// </summary>
    Task<RestoreResultDto> RestoreBackupAsync(string filePath, string? password = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

