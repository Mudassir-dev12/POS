using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Service implementing safe atomic database restores, schema compatibility verification, and PreRestore safety snapshots.
/// </summary>
public class RestoreService : IRestoreService
{
    private readonly AppDbContext _dbContext;
    private readonly IAppPaths _appPaths;
    private readonly IBackupService _backupService;
    private readonly IBackupCryptoService _cryptoService;
    private readonly ILogger<RestoreService> _logger;
    private static readonly SemaphoreSlim _restoreLock = new(1, 1);

    public RestoreService(
        AppDbContext dbContext,
        IAppPaths appPaths,
        IBackupService backupService,
        IBackupCryptoService cryptoService,
        ILogger<RestoreService>? logger = null)
    {
        _dbContext = dbContext;
        _appPaths = appPaths;
        _backupService = backupService;
        _cryptoService = cryptoService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RestoreService>.Instance;
    }

    public async Task<BackupValidationResultDto> ValidateBackupForRestoreAsync(string filePath, string? password = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new BackupValidationResultDto
            {
                IsValid = false,
                ErrorMessage = $"Backup file not found at '{filePath}'."
            };
        }

        bool isEncrypted = _cryptoService.IsEncryptedBackup(filePath);
        if (isEncrypted && string.IsNullOrWhiteSpace(password))
        {
            return new BackupValidationResultDto
            {
                IsValid = false,
                IsEncrypted = true,
                RequiresPassword = true,
                ErrorMessage = "This backup is encrypted and requires a password to preview and restore."
            };
        }

        string tempDir = Path.Combine(_appPaths.TempDirectory, $"restore_val_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string zipPath = filePath;
            if (isEncrypted)
            {
                zipPath = Path.Combine(tempDir, "decrypted.zip");
                try
                {
                    _cryptoService.DecryptFile(filePath, zipPath, password!);
                }
                catch (Exception ex)
                {
                    return new BackupValidationResultDto
                    {
                        IsValid = false,
                        IsEncrypted = true,
                        RequiresPassword = true,
                        ErrorMessage = $"Failed to decrypt backup: {ex.Message}"
                    };
                }
            }

            using var zipArchive = ZipFile.OpenRead(zipPath);
            var manifestEntry = zipArchive.GetEntry("manifest.json");
            var dbEntry = zipArchive.GetEntry("shop.db");

            if (dbEntry == null)
            {
                return new BackupValidationResultDto
                {
                    IsValid = false,
                    ErrorMessage = "The backup archive is missing 'shop.db'."
                };
            }

            BackupManifest? manifest = null;
            if (manifestEntry != null)
            {
                using var stream = manifestEntry.Open();
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, cancellationToken: cancellationToken);
            }

            // Extract shop.db to temp to test checksum and integrity
            string tempDbPath = Path.Combine(tempDir, "shop.db");
            dbEntry.ExtractToFile(tempDbPath, overwrite: true);

            string extractedSha256;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(tempDbPath))
            {
                extractedSha256 = Convert.ToHexString(await sha.ComputeHashAsync(stream, cancellationToken));
            }

            if (manifest != null && !string.IsNullOrWhiteSpace(manifest.DatabaseSha256))
            {
                if (!string.Equals(manifest.DatabaseSha256, extractedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return new BackupValidationResultDto
                    {
                        IsValid = false,
                        ErrorMessage = "Corrupted backup: database checksum inside the archive does not match the manifest."
                    };
                }
            }

            // Quick integrity check on extracted database
            using (var testConn = new SqliteConnection($"Data Source={tempDbPath};Mode=ReadOnly;Pooling=False"))
            {
                await testConn.OpenAsync(cancellationToken);
                using var cmd = testConn.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var res = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();
                if (!string.Equals(res, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return new BackupValidationResultDto
                    {
                        IsValid = false,
                        ErrorMessage = $"Database integrity check failed: {res}"
                    };
                }
            }

            // Compare schema versions
            string currentSchema = string.Empty;
            var currentCounts = new Dictionary<string, int>();

            try
            {
                var migrations = _dbContext.Database.GetMigrations().ToList();
                currentSchema = migrations.LastOrDefault() ?? "Initial";

                currentCounts["Products"] = await _dbContext.Products.CountAsync(cancellationToken);
                currentCounts["Sales"] = await _dbContext.Sales.CountAsync(cancellationToken);
                currentCounts["Purchases"] = await _dbContext.Purchases.CountAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not query current database counts.");
            }

            bool isNewer = false;
            bool isOlder = false;

            if (manifest != null && !string.IsNullOrWhiteSpace(manifest.SchemaVersion))
            {
                var allKnownMigrations = _dbContext.Database.GetMigrations().ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!allKnownMigrations.Contains(manifest.SchemaVersion) && !string.Equals(manifest.SchemaVersion, "Legacy", StringComparison.OrdinalIgnoreCase))
                {
                    // The schema version in the backup is not known to this application -> backup is from a newer app version!
                    isNewer = true;
                }
                else if (!string.Equals(manifest.SchemaVersion, currentSchema, StringComparison.OrdinalIgnoreCase))
                {
                    isOlder = true;
                }
            }

            if (isNewer)
            {
                return new BackupValidationResultDto
                {
                    IsValid = false,
                    FilePath = filePath,
                    IsEncrypted = isEncrypted,
                    Manifest = manifest,
                    CurrentDatabaseRowCounts = currentCounts,
                    CurrentSchemaVersion = currentSchema,
                    SchemaIsNewerThanApp = true,
                    ErrorMessage = $"This backup was created by a newer version of ShopPOS (Schema: {manifest?.SchemaVersion}). Please update ShopPOS to restore this backup."
                };
            }

            return new BackupValidationResultDto
            {
                IsValid = true,
                FilePath = filePath,
                IsEncrypted = isEncrypted,
                RequiresPassword = false,
                Manifest = manifest,
                CurrentDatabaseRowCounts = currentCounts,
                CurrentSchemaVersion = currentSchema,
                SchemaIsNewerThanApp = false,
                SchemaIsOlderThanApp = isOlder
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating backup {FilePath}", filePath);
            return new BackupValidationResultDto
            {
                IsValid = false,
                FilePath = filePath,
                IsEncrypted = isEncrypted,
                ErrorMessage = $"Validation failed: {ex.Message}"
            };
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    public async Task<RestoreResultDto> RestoreBackupAsync(string filePath, string? password = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _restoreLock.WaitAsync(cancellationToken);
        try
        {
            progress?.Report("Validating backup archive...");
            var validation = await ValidateBackupForRestoreAsync(filePath, password, cancellationToken);
            if (!validation.IsValid)
            {
                return new RestoreResultDto
                {
                    Success = false,
                    ErrorMessage = validation.ErrorMessage ?? "Backup validation failed."
                };
            }

            // 1. Create PreRestore safety backup of current database first
            progress?.Report("Creating Pre-Restore safety backup of current data...");
            string? preRestoreFileName = null;

            if (File.Exists(_appPaths.DatabasePath))
            {
                var preBackupResult = await _backupService.CreateBackupAsync(BackupKind.PreRestore, cancellationToken: cancellationToken);
                if (!preBackupResult.Success)
                {
                    string err = $"Pre-Restore safety backup failed: {preBackupResult.ErrorMessage}. Restore aborted to protect current data.";
                    _logger.LogError("{Error}", err);
                    return new RestoreResultDto
                    {
                        Success = false,
                        ErrorMessage = err
                    };
                }
                preRestoreFileName = preBackupResult.FileName;
                _logger.LogInformation("Pre-Restore safety backup created: {FileName}", preRestoreFileName);
            }

            // 2. Prepare extracted files in temp directory
            progress?.Report("Extracting replacement database and catalog images...");
            string tempDir = Path.Combine(_appPaths.TempDirectory, $"restore_exec_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string zipPath = filePath;
                if (validation.IsEncrypted)
                {
                    zipPath = Path.Combine(tempDir, "decrypted.zip");
                    _cryptoService.DecryptFile(filePath, zipPath, password!);
                }

                using var zipArchive = ZipFile.OpenRead(zipPath);
                var dbEntry = zipArchive.GetEntry("shop.db");
                if (dbEntry == null)
                {
                    throw new InvalidOperationException("Backup archive has no 'shop.db'.");
                }

                string extractedDbPath = Path.Combine(tempDir, "shop.db");
                dbEntry.ExtractToFile(extractedDbPath, overwrite: true);

                // Run integrity and foreign key checks on extracted database before replacing live db
                using (var testConn = new SqliteConnection($"Data Source={extractedDbPath};Mode=ReadOnly;Pooling=False"))
                {
                    await testConn.OpenAsync(cancellationToken);
                    using var cmd1 = testConn.CreateCommand();
                    cmd1.CommandText = "PRAGMA integrity_check;";
                    var checkRes = (await cmd1.ExecuteScalarAsync(cancellationToken))?.ToString();
                    if (!string.Equals(checkRes, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Extracted replacement database failed integrity check: {checkRes}");
                    }

                    using var cmd2 = testConn.CreateCommand();
                    cmd2.CommandText = "PRAGMA foreign_key_check;";
                    using var reader = await cmd2.ExecuteReaderAsync(cancellationToken);
                    if (reader.HasRows)
                    {
                        throw new InvalidOperationException("Extracted replacement database has foreign key integrity violations.");
                    }
                }

                // 3. Close SQLite connections and clear pool
                progress?.Report("Closing database connection pools...");
                try { await _dbContext.Database.CloseConnectionAsync(); } catch { }
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                // 4. Atomically replace shop.db
                progress?.Report("Atomically replacing shop.db...");
                string liveDb = _appPaths.DatabasePath;
                string oldBackupDb = liveDb + ".old";

                _appPaths.EnsureDirectoriesExist();

                if (File.Exists(liveDb))
                {
                    if (File.Exists(oldBackupDb)) DeleteWithRetry(oldBackupDb);
                    MoveWithRetry(liveDb, oldBackupDb);
                }

                try
                {
                    File.Copy(extractedDbPath, liveDb, overwrite: true);

                    // Delete old -wal and -shm files
                    string wal = liveDb + "-wal";
                    string shm = liveDb + "-shm";
                    if (File.Exists(wal)) try { File.Delete(wal); } catch { }
                    if (File.Exists(shm)) try { File.Delete(shm); } catch { }

                    // If successfully placed, remove old db backup
                    if (File.Exists(oldBackupDb))
                    {
                        try { File.Delete(oldBackupDb); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Failed while copying replacement database to destination. Rolling back original database.");
                    if (File.Exists(oldBackupDb))
                    {
                        File.Move(oldBackupDb, liveDb);
                    }
                    throw;
                }

                // 5. Restore Images/ folder
                progress?.Report("Restoring catalog images...");
                foreach (var entry in zipArchive.Entries)
                {
                    if (entry.FullName.StartsWith("Images/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Name))
                    {
                        string relative = entry.FullName["Images/".Length..];
                        string destImagePath = Path.Combine(_appPaths.ImagesDirectory, relative);
                        string? parent = Path.GetDirectoryName(destImagePath);
                        if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
                        {
                            Directory.CreateDirectory(parent);
                        }
                        entry.ExtractToFile(destImagePath, overwrite: true);
                    }
                }

                // 6. Run pending migrations if restored schema was older
                if (validation.SchemaIsOlderThanApp)
                {
                    progress?.Report("Migrating restored database to latest schema...");
                    try
                    {
                        await _dbContext.Database.MigrateAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while migrating restored database to current schema.");
                    }
                }

                _dbContext.ChangeTracker.Clear();
                progress?.Report("Database restored successfully!");
                _logger.LogInformation("Database restored successfully from {FilePath}.", filePath);

                int prodCount = validation.Manifest?.TableRowCounts.GetValueOrDefault("Products", 0) ?? 0;
                int saleCount = validation.Manifest?.TableRowCounts.GetValueOrDefault("Sales", 0) ?? 0;
                int purchaseCount = validation.Manifest?.TableRowCounts.GetValueOrDefault("Purchases", 0) ?? 0;

                return new RestoreResultDto
                {
                    Success = true,
                    PreRestoreBackupFileName = preRestoreFileName,
                    RestoredProducts = prodCount,
                    RestoredSales = saleCount,
                    RestoredPurchases = purchaseCount,
                    RequiresRestart = true,
                    SummaryMessage = $"Successfully restored {prodCount} products, {saleCount} sales, and {purchaseCount} purchases."
                };
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore operation failed for {FilePath}", filePath);
            return new RestoreResultDto
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            _restoreLock.Release();
        }
    }
    private static void DeleteWithRetry(string path, int maxRetries = 10, int delayMs = 100)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                Thread.Sleep(delayMs);
            }
        }
    }

    private static void MoveWithRetry(string source, string destination, int maxRetries = 10, int delayMs = 100)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                File.Move(source, destination);
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                Thread.Sleep(delayMs);
            }
        }
    }

}
