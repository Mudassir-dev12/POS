using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Executes pre-startup environment, lock, and schema safety validation before opening the UI.
/// </summary>
public class StartupSafetyChecker : IStartupSafetyChecker
{
    private const long MinimumFreeDiskSpaceBytes = 500L * 1024 * 1024; // 500 MB
    private readonly ILogger<StartupSafetyChecker> _logger;

    public StartupSafetyChecker(ILogger<StartupSafetyChecker>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StartupSafetyChecker>.Instance;
    }

    public StartupCheckResult ValidateFileSystem(string dataDirectory)
    {
        // 1. Check folder writability
        try
        {
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            string testFile = Path.Combine(dataDirectory, $".startup_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data directory '{DataDirectory}' is not writable.", dataDirectory);
            return StartupCheckResult.Blocked(
                StartupIssueType.FolderNotWritable,
                $"The application data folder at '{dataDirectory}' is not writable.\n\nPlease ensure your user account has write permissions to this directory or choose another location.");
        }

        // 2. Check disk free space
        try
        {
            string fullPath = Path.GetFullPath(dataDirectory);
            string? root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                long freeSpace = drive.AvailableFreeSpace;

                if (freeSpace < MinimumFreeDiskSpaceBytes)
                {
                    double freeMb = freeSpace / (1024.0 * 1024.0);
                    _logger.LogWarning("Low disk space detected: {FreeMb:F1} MB free on drive {Root}", freeMb, root);
                    return StartupCheckResult.Blocked(
                        StartupIssueType.LowDiskSpace,
                        $"Critically low disk space on drive '{root}'. Available space: {freeMb:F1} MB (minimum required: 500 MB).\n\nPlease free up disk space to prevent database corruption.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect drive free space.");
        }

        return StartupCheckResult.Success();
    }

    public StartupCheckResult CheckDatabaseLock(string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            return StartupCheckResult.Success();
        }

        try
        {
            using var fs = new FileStream(databasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            return StartupCheckResult.Success();
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Database file '{DbPath}' is locked by another process.", databasePath);
            return StartupCheckResult.Blocked(
                StartupIssueType.DatabaseLocked,
                $"The database file at '{databasePath}' is locked by another running instance of ShopPOS or another program.\n\nPlease close any other copies of ShopPOS and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open database file to verify lock status.");
            return StartupCheckResult.Success();
        }
    }

    public async Task<StartupCheckResult> ValidateDatabaseAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(databasePath))
        {
            return StartupCheckResult.Success();
        }

        try
        {
            var connStrBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        using var connection = new SqliteConnection(connStrBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. Run PRAGMA quick_check
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA quick_check;";
            var checkResult = (string?)await cmd.ExecuteScalarAsync(cancellationToken);
            if (!string.Equals(checkResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Database integrity check failed: {Result}", checkResult);
                return StartupCheckResult.Corrupted(
                    $"Database integrity quick_check failed with message: '{checkResult}'.\n\nThe database may be corrupted. Would you like to restore from your latest verified backup?");
            }
        }

        // 2. Check migrations history
        bool historyTableExists = false;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
            long count = (long)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
            historyTableExists = count > 0;
        }

        if (!historyTableExists)
        {
            // Empty / unmigrated database
            return StartupCheckResult.OlderVersion(new List<string> { "InitialCreate" });
        }

        var appliedMigrations = new List<string>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                appliedMigrations.Add(reader.GetString(0));
            }
        }

        // Get compiled migrations from AppDbContext
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(connStrBuilder.ConnectionString);
        using var tempContext = new AppDbContext(optionsBuilder.Options);

        var migrationsAssembly = tempContext.GetService<IMigrationsAssembly>();
        var allAppMigrations = migrationsAssembly.Migrations.Keys.OrderBy(m => m).ToList();

        // Check if database contains migrations newer than what this app knows
        var foreignOrNewerMigrations = appliedMigrations.Except(allAppMigrations).ToList();
        if (foreignOrNewerMigrations.Any())
        {
            _logger.LogError("Database is newer than application. Foreign migrations: {Migrations}", string.Join(", ", foreignOrNewerMigrations));
            return StartupCheckResult.Blocked(
                StartupIssueType.DatabaseNewerThanApp,
                $"This database was created or updated by a newer version of ShopPOS (found newer schema '{foreignOrNewerMigrations.First()}').\n\nPlease update ShopPOS to the latest version to open this database.");
        }

        // Check if application has pending migrations to apply
        var pendingMigrations = allAppMigrations.Except(appliedMigrations).ToList();
        if (pendingMigrations.Any())
        {
            return StartupCheckResult.OlderVersion(pendingMigrations);
        }

        return StartupCheckResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inspect database file '{DbPath}'. Marking as corrupted.", databasePath);
            return StartupCheckResult.Corrupted(
                $"Database inspection failed: {ex.Message}.\n\nThe database may be corrupted. Would you like to restore from your latest verified backup?");
        }
    }
}

