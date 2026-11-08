using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Implements database health checks, WAL checkpoints, startup lock file tracking, and rolling log purging.
/// </summary>
public class DatabaseSafetyService : IDatabaseSafetyService
{
    private readonly IAppPaths _appPaths;
    private readonly ILogger<DatabaseSafetyService> _logger;

    public DatabaseSafetyService(IAppPaths appPaths, ILogger<DatabaseSafetyService>? logger = null)
    {
        _appPaths = appPaths;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DatabaseSafetyService>.Instance;
    }

    public async Task<bool> RunQuickCheckAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_appPaths.DatabasePath))
        {
            return true;
        }

        try
        {
            using var connection = new SqliteConnection($"Data Source={_appPaths.DatabasePath}");
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check;";

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            string? status = result?.ToString();

            if (string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Database integrity quick_check passed successfully.");
                return true;
            }

            _logger.LogWarning("Database integrity quick_check returned non-ok status: {Status}", status);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute database quick_check.");
            return false;
        }
    }

    public async Task RunWalCheckpointAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_appPaths.DatabasePath))
        {
            return;
        }

        try
        {
            using var connection = new SqliteConnection($"Data Source={_appPaths.DatabasePath}");
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Database WAL checkpoint (TRUNCATE) completed successfully on application exit.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute database WAL checkpoint on exit.");
        }
    }

    public bool CheckAndCreateLockFile()
    {
        _appPaths.EnsureDirectoriesExist();
        string lockPath = _appPaths.LockFilePath;
        bool wasUnclean = File.Exists(lockPath);

        if (wasUnclean)
        {
            _logger.LogWarning("Unclean application shutdown detected. Lock file existed at: {LockPath}", lockPath);
        }

        try
        {
            string content = $"ProcessId={Environment.ProcessId}\nStartedAt={DateTime.UtcNow:O}\nMachine={Environment.MachineName}";
            File.WriteAllText(lockPath, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not create application startup lock file at {LockPath}", lockPath);
        }

        return wasUnclean;
    }

    public void RemoveLockFile()
    {
        try
        {
            string lockPath = _appPaths.LockFilePath;
            if (File.Exists(lockPath))
            {
                File.Delete(lockPath);
                _logger.LogInformation("Application lock file cleanly removed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove application lock file on clean shutdown.");
        }
    }

    public int PurgeOldLogs(int retentionDays = 30)
    {
        string logsDir = _appPaths.LogsDirectory;
        if (!Directory.Exists(logsDir)) return 0;

        int deletedCount = 0;
        var threshold = DateTime.UtcNow.AddDays(-retentionDays);

        try
        {
            var files = Directory.GetFiles(logsDir, "*.log");
            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                if (fi.LastWriteTimeUtc < threshold)
                {
                    try
                    {
                        fi.Delete();
                        deletedCount++;
                        _logger.LogInformation("Purged expired log file: {FileName}", fi.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete old log file: {FileName}", fi.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while purging old log files in {LogsDir}", logsDir);
        }

        return deletedCount;
    }
}
