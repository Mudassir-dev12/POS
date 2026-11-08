namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Service managing SQLite database health checks, WAL checkpoints on exit, startup lock file tracking, and old log purging.
/// </summary>
public interface IDatabaseSafetyService
{
    /// <summary>
    /// Executes PRAGMA quick_check at startup to ensure SQLite database integrity.
    /// Returns true if database is healthy; false if corruption was detected.
    /// </summary>
    Task<bool> RunQuickCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes PRAGMA wal_checkpoint(TRUNCATE) on clean application exit to flush WAL to shop.db and truncate WAL file.
    /// </summary>
    Task RunWalCheckpointAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects whether the previous application run ended uncleanly (e.g. crash or power loss) via lock file existence.
    /// </summary>
    bool CheckAndCreateLockFile();

    /// <summary>
    /// Removes the lock file upon clean application exit.
    /// </summary>
    void RemoveLockFile();

    /// <summary>
    /// Purges log files older than the specified retention threshold (default 30 days).
    /// Returns the count of deleted log files.
    /// </summary>
    int PurgeOldLogs(int retentionDays = 30);
}

