using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShopPOS.Core.Interfaces;

public enum StartupIssueType
{
    None,
    FolderNotWritable,
    LowDiskSpace,
    DatabaseLocked,
    DatabaseNewerThanApp,
    DatabaseOlderNeedsMigration,
    DatabaseCorrupted
}

public class StartupCheckResult
{
    public bool CanProceed { get; set; } = true;
    public StartupIssueType Issue { get; set; } = StartupIssueType.None;
    public string? Message { get; set; }
    public bool NeedsMigration { get; set; }
    public bool NeedsRestorePrompt { get; set; }
    public List<string> PendingMigrations { get; set; } = new();

    public static StartupCheckResult Success() => new() { CanProceed = true };

    public static StartupCheckResult Blocked(StartupIssueType issue, string message) =>
        new() { CanProceed = false, Issue = issue, Message = message };

    public static StartupCheckResult OlderVersion(List<string> pendingMigrations) =>
        new()
        {
            CanProceed = true,
            Issue = StartupIssueType.DatabaseOlderNeedsMigration,
            NeedsMigration = true,
            PendingMigrations = pendingMigrations,
            Message = $"The database requires {pendingMigrations.Count} update(s) to match the current application version."
        };

    public static StartupCheckResult Corrupted(string message) =>
        new()
        {
            CanProceed = false,
            Issue = StartupIssueType.DatabaseCorrupted,
            NeedsRestorePrompt = true,
            Message = message
        };
}

/// <summary>
/// Pre-flight startup validator ensuring data directory integrity, schema compatibility, and file locks.
/// </summary>
public interface IStartupSafetyChecker
{
    /// <summary>
    /// Validates data directory write permissions and free space threshold (500 MB).
    /// </summary>
    StartupCheckResult ValidateFileSystem(string dataDirectory);

    /// <summary>
    /// Verifies that the database file is not exclusively locked by another process.
    /// </summary>
    StartupCheckResult CheckDatabaseLock(string databasePath);

    /// <summary>
    /// Checks database schema version vs application migrations, and tests PRAGMA integrity.
    /// </summary>
    Task<StartupCheckResult> ValidateDatabaseAsync(string databasePath, CancellationToken cancellationToken = default);
}

