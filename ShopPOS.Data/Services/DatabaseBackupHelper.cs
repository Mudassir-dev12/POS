using System.Diagnostics;
using System.IO;

namespace ShopPOS.Data.Services;

/// <summary>
/// Provides safe pre-migration database backup functionality.
/// Creates a timestamped copy of the database before applying destructive migrations.
/// </summary>
public static class DatabaseBackupHelper
{
    /// <summary>
    /// Creates a pre-migration backup of the SQLite database.
    /// If the database file does not exist, no action is taken.
    /// If the backup directory cannot be created or file copy fails, an exception is thrown to abort the migration.
    /// </summary>
    /// <param name="dbPath">The absolute path to the SQLite database.</param>
    /// <param name="migrationName">The name of the pending migration.</param>
    /// <param name="backupDirectory">Optional custom backup directory. If null, defaults to a 'Backups' subfolder next to the database file.</param>
    /// <returns>The path to the created backup file, or null if the database did not exist.</returns>
    public static string? CreatePreMigrationBackup(string dbPath, string migrationName, string? backupDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
        {
            Trace.WriteLine($"DatabaseBackupHelper: No existing database found at '{dbPath}'. Skipping pre-migration backup.");
            return null;
        }

        string backupDir = !string.IsNullOrWhiteSpace(backupDirectory)
            ? backupDirectory
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".", "Backups");

        try
        {
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backupFileName = $"pre-migration-{migrationName}-{timestamp}.db";
            string backupFilePath = Path.Combine(backupDir, backupFileName);

            try
            {
                using var srcConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                srcConn.Open();
                using var dstConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={backupFilePath}");
                dstConn.Open();
                srcConn.BackupDatabase(dstConn);
            }
            catch
            {
                File.Copy(dbPath, backupFilePath, overwrite: false);
            }

            Trace.WriteLine($"Pre-migration backup successfully created at '{backupFilePath}' for migration '{migrationName}'.");

            return backupFilePath;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"FATAL: Pre-migration database backup failed for '{dbPath}' before migration '{migrationName}'. Aborting migration to protect data. Error: {ex}");
            throw new InvalidOperationException(
                $"Pre-migration database backup failed: {ex.Message}. Migration '{migrationName}' was aborted to prevent data loss.",
                ex);
        }
    }
}
