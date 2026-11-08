using System.IO;

using System.IO.Compression;

using System.Security.Cryptography;

using System.Text.Json;

using Microsoft.Data.Sqlite;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

using ShopPOS.Core.DTOs.Backup;

using ShopPOS.Core.Entities;

using ShopPOS.Core.Enums;

using ShopPOS.Core.Interfaces;



namespace ShopPOS.Data.Services;



/// <summary>

/// Service implementing SQLite online database snapshots, GFS retention, verification, and backup archives.

/// </summary>

public class BackupService : IBackupService

{

    private readonly AppDbContext _dbContext;

    private readonly IAppPaths _appPaths;

    private readonly IBackupCryptoService _cryptoService;

    private readonly IBackupUploader? _backupUploader;

    private readonly ILogger<BackupService> _logger;

    private static readonly SemaphoreSlim _backupLock = new(1, 1);



    public BackupService(

        AppDbContext dbContext,

        IAppPaths appPaths,

        IBackupCryptoService cryptoService,

        IBackupUploader? backupUploader = null,

        ILogger<BackupService>? logger = null)

    {

        _dbContext = dbContext;

        _appPaths = appPaths;

        _cryptoService = cryptoService;

        _backupUploader = backupUploader;

        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupService>.Instance;

    }



    public async Task<BackupResultDto> CreateBackupAsync(BackupKind kind, string? password = null, CancellationToken cancellationToken = default)

    {

        await _backupLock.WaitAsync(cancellationToken);

        try

        {

            _appPaths.EnsureDirectoriesExist();



            if (!File.Exists(_appPaths.DatabasePath))

            {

                return new BackupResultDto

                {

                    Success = false,

                    ErrorMessage = $"Database file does not exist at '{_appPaths.DatabasePath}'."

                };

            }



            var dbFileInfo = new FileInfo(_appPaths.DatabasePath);

            long requiredSpace = Math.Max(10 * 1024 * 1024, dbFileInfo.Length * 2); // At least 2x DB size or 10MB



            try

            {

                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(_appPaths.BackupsDirectory))!);

                if (drive.AvailableFreeSpace < requiredSpace)

                {

                    string err = $"Insufficient disk space on drive {drive.Name}. Required: {requiredSpace / (1024 * 1024)} MB, Available: {drive.AvailableFreeSpace / (1024 * 1024)} MB.";

                    _logger.LogError("{Error}", err);

                    await RecordBackupFailureAsync(err, cancellationToken);

                    return new BackupResultDto { Success = false, ErrorMessage = err };

                }

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Could not check available free disk space.");

            }



            // Retrieve settings

            var settings = await _dbContext.ShopSettings.FirstOrDefaultAsync(cancellationToken) ?? new ShopSetting();

            string shopName = string.IsNullOrWhiteSpace(settings.ShopName) ? "ShopPOS" : settings.ShopName;

            string shopSlug = SanitizeFileName(shopName);

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

            string kindSlug = kind.ToString().ToLowerInvariant();



            bool shouldEncrypt = !string.IsNullOrWhiteSpace(password);

            string finalExtension = shouldEncrypt ? ".zip.enc" : ".zip";

            string finalFileName = $"ShopPOS_{shopSlug}_{timestamp}_{kindSlug}{finalExtension}";

            string finalFilePath = Path.Combine(_appPaths.BackupsDirectory, finalFileName);

            string tempZipPath = Path.Combine(_appPaths.TempDirectory, $"{Guid.NewGuid():N}.zip.tmp");



            // 1. Take WAL-safe online snapshot of SQLite database

            string tempDbPath = Path.Combine(_appPaths.TempDirectory, $"{Guid.NewGuid():N}.db");

            string schemaVersion = string.Empty;

            var tableCounts = new Dictionary<string, int>();

            string dbSha256 = string.Empty;



            try

            {

                using (var srcConn = new SqliteConnection($"Data Source={_appPaths.DatabasePath};Mode=ReadOnly;Pooling=False"))

                {

                    await srcConn.OpenAsync(cancellationToken);

                    using (var dstConn = new SqliteConnection($"Data Source={tempDbPath};Pooling=False"))

                    {

                        await dstConn.OpenAsync(cancellationToken);

                        srcConn.BackupDatabase(dstConn);

                    }

                }



                // 2. Verify snapshot integrity

                using (var testConn = new SqliteConnection($"Data Source={tempDbPath};Mode=ReadOnly;Pooling=False"))

                {

                    await testConn.OpenAsync(cancellationToken);



                    using (var cmd = testConn.CreateCommand())

                    {

                        cmd.CommandText = "PRAGMA integrity_check;";

                        var checkRes = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();

                        if (!string.Equals(checkRes, "ok", StringComparison.OrdinalIgnoreCase))

                        {

                            throw new InvalidOperationException($"Snapshot database failed integrity check: {checkRes}");

                        }

                    }



                    using (var cmd = testConn.CreateCommand())

                    {

                        cmd.CommandText = "PRAGMA foreign_key_check;";

                        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                        if (reader.HasRows)

                        {

                            throw new InvalidOperationException("Snapshot database has foreign key integrity violations.");

                        }

                    }



                    // Query latest migration

                    try

                    {

                        using var cmd = testConn.CreateCommand();

                        cmd.CommandText = "SELECT MigrationId FROM \"__EFMigrationsHistory\" ORDER BY MigrationId DESC LIMIT 1;";

                        schemaVersion = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString() ?? string.Empty;

                    }

                    catch

                    {

                        schemaVersion = "Initial";

                    }



                    // Record row counts of key tables

                    string[] tables = { "Products", "Sales", "Purchases", "Categories", "Suppliers", "Expenses", "StockAdjustments" };

                    foreach (var tbl in tables)

                    {

                        try

                        {

                            using var cmd = testConn.CreateCommand();

                            cmd.CommandText = $"SELECT COUNT(*) FROM \"{tbl}\";";

                            var cnt = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));

                            tableCounts[tbl] = cnt;

                        }

                        catch

                        {

                            tableCounts[tbl] = 0;

                        }

                    }

                }



                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();



                // Compute SHA256 of snapshot database

                using (var sha = SHA256.Create())

                using (var stream = File.OpenRead(tempDbPath))

                {

                    dbSha256 = Convert.ToHexString(await sha.ComputeHashAsync(stream, cancellationToken));

                }



                // 3. Package manifest.json, shop.db, and Images folder into zip

                var manifest = new BackupManifest

                {

                    AppVersion = typeof(ShopSetting).Assembly.GetName().Version?.ToString() ?? "1.0.0",

                    SchemaVersion = schemaVersion,

                    CreatedAt = DateTime.UtcNow,

                    MachineName = Environment.MachineName,

                    ShopName = settings.ShopName,

                    TableRowCounts = tableCounts,

                    DatabaseSha256 = dbSha256,

                    IsEncrypted = shouldEncrypt

                };



                using (var zipArchive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))

                {

                    // Add shop.db

                    zipArchive.CreateEntryFromFile(tempDbPath, "shop.db", CompressionLevel.Optimal);



                    // Add manifest.json

                    var manifestEntry = zipArchive.CreateEntry("manifest.json", CompressionLevel.Optimal);

                    using (var entryStream = manifestEntry.Open())

                    {

                        await JsonSerializer.SerializeAsync(entryStream, manifest, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);

                    }



                    // Add Images folder if present, non-empty, and not excluded by cloud backup settings
                    if (Directory.Exists(_appPaths.ImagesDirectory) && !(settings.GitHubEnabled && settings.GitHubExcludeImages))

                    {

                        var imageFiles = Directory.GetFiles(_appPaths.ImagesDirectory, "*.*", SearchOption.AllDirectories);

                        foreach (var imgFile in imageFiles)

                        {

                            string relativePath = Path.GetRelativePath(_appPaths.ImagesDirectory, imgFile);

                            zipArchive.CreateEntryFromFile(imgFile, Path.Combine("Images", relativePath).Replace('\\', '/'), CompressionLevel.Fastest);

                        }

                    }

                }



                // 4. Handle optional encryption

                string packageToFinalize = tempZipPath;

                string? tempEncryptedPath = null;



                if (shouldEncrypt)

                {

                    string effectivePassword = !string.IsNullOrWhiteSpace(password)

                        ? password

                        : throw new InvalidOperationException("Encryption is enabled but no password was provided for backup.");



                    tempEncryptedPath = Path.Combine(_appPaths.TempDirectory, $"{Guid.NewGuid():N}.enc.tmp");

                    _cryptoService.EncryptFile(tempZipPath, tempEncryptedPath, effectivePassword);

                    packageToFinalize = tempEncryptedPath;

                }



                // 5. Atomic write to final destination in Backups folder

                string tempDestPath = finalFilePath + ".tmp";

                File.Copy(packageToFinalize, tempDestPath, overwrite: true);

                if (File.Exists(finalFilePath)) File.Delete(finalFilePath);

                File.Move(tempDestPath, finalFilePath);



                // Compute SHA256 and size of the final archive file

                long finalSize = new FileInfo(finalFilePath).Length;

                string finalSha256;

                using (var sha = SHA256.Create())

                using (var fs = File.OpenRead(finalFilePath))

                {

                    finalSha256 = Convert.ToHexString(await sha.ComputeHashAsync(fs, cancellationToken));

                }



                // Compute MD5 for cloud upload verification

                string finalMd5;

                using (var md5 = MD5.Create())

                using (var fs = File.OpenRead(finalFilePath))

                {

                    finalMd5 = Convert.ToHexString(await md5.ComputeHashAsync(fs, cancellationToken));

                }



                var initialDriveStatus = settings.GoogleDriveEnabled
                    ? DriveUploadStatus.Pending
                    : DriveUploadStatus.NotConfigured;

                var initialGitHubStatus = settings.GitHubEnabled
                    ? GitHubUploadStatus.Pending
                    : GitHubUploadStatus.NotConfigured;

                // 6. Record in Database
                var record = new BackupRecord
                {
                    FileName = finalFileName,
                    FilePath = finalFilePath,
                    SizeBytes = finalSize,
                    Sha256 = finalSha256,
                    Kind = kind,
                    IsEncrypted = shouldEncrypt,
                    SchemaVersion = schemaVersion,
                    AppVersion = manifest.AppVersion,
                    VerifyStatus = BackupVerifyStatus.NotVerified,
                    Status = BackupStatus.Created,
                    DriveStatus = initialDriveStatus,
                    GitHubStatus = initialGitHubStatus,
                    DriveMd5Checksum = finalMd5,
                    IsDeletedLocally = false
                };



                _dbContext.BackupRecords.Add(record);



                // Update ShopSetting

                settings.LastSuccessfulBackupAt = DateTime.UtcNow;

                settings.LastBackupError = null;

                await _dbContext.SaveChangesAsync(cancellationToken);



                // Trigger cloud backup upload queue if enabled (Google Drive or GitHub)
                if ((settings.GoogleDriveEnabled || settings.GitHubEnabled) && _backupUploader != null)

                {

                    _ = Task.Run(async () =>

                    {

                        try

                        {

                            await _backupUploader.ProcessQueueAsync(force: false, CancellationToken.None);

                        }

                        catch (Exception ex)

                        {

                            _logger.LogWarning(ex, "Failed to trigger Google Drive upload queue after backup creation.");

                        }

                    });

                }



                string? warningMessage = null;



                // 7. Copy to Secondary Folder if configured

                if (!string.IsNullOrWhiteSpace(settings.SecondFolderPath))

                {

                    try

                    {

                        if (Directory.Exists(settings.SecondFolderPath))

                        {

                            string secondDest = Path.Combine(settings.SecondFolderPath, finalFileName);

                            File.Copy(finalFilePath, secondDest, overwrite: true);

                            _logger.LogInformation("Successfully mirrored backup to secondary folder: {Dest}", secondDest);

                        }

                        else

                        {

                            warningMessage = $"Secondary backup folder '{settings.SecondFolderPath}' is not reachable.";

                            _logger.LogWarning("{Warning}", warningMessage);

                        }

                    }

                    catch (Exception ex)

                    {

                        warningMessage = $"Failed to copy backup to secondary folder: {ex.Message}";

                        _logger.LogWarning(ex, "Failed to copy backup to secondary folder: {Folder}", settings.SecondFolderPath);

                    }

                }



                // 8. Run automatic post-creation verification in background

                // 8. Run automatic post-creation verification
                try
                {
                    await VerifyBackupFileInternalAsync(finalFilePath, shouldEncrypt ? password : null, record.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Post-creation verification failed for backup {FileName}", finalFileName);
                }



                // 9. Run GFS Retention Policy

                try

                {

                    await RunRetentionAsync(cancellationToken);

                }

                catch (Exception ex)

                {

                    _logger.LogWarning(ex, "Error running retention policy after backup.");

                }



                _logger.LogInformation("Backup created successfully: {FileName} ({Size} bytes, Kind: {Kind})", finalFileName, finalSize, kind);



                var dto = MapToDto(record);

                return new BackupResultDto

                {

                    Success = true,

                    FileName = finalFileName,

                    FilePath = finalFilePath,

                    SizeBytes = finalSize,

                    Sha256 = finalSha256,

                    Record = dto,

                    WarningMessage = warningMessage

                };

            }

            finally

            {

                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();



                // Clean up temporary snapshot and zip files

                if (File.Exists(tempDbPath))

                {

                    try { File.Delete(tempDbPath); } catch { }

                }

                if (File.Exists(tempZipPath))

                {

                    try { File.Delete(tempZipPath); } catch { }

                }

            }

        }

        catch (Exception ex)

        {

            _logger.LogError(ex, "Failed to create database backup (Kind: {Kind})", kind);

            await RecordBackupFailureAsync(ex.Message, cancellationToken);

            return new BackupResultDto

            {

                Success = false,

                ErrorMessage = ex.Message

            };

        }

        finally

        {

            _backupLock.Release();

        }

    }



    private async Task RecordBackupFailureAsync(string error, CancellationToken cancellationToken)

    {

        try

        {

            var settings = await _dbContext.ShopSettings.FirstOrDefaultAsync(cancellationToken);

            if (settings != null)

            {

                settings.LastBackupError = error;

                await _dbContext.SaveChangesAsync(cancellationToken);

            }

        }

        catch (Exception ex)

        {

            _logger.LogError(ex, "Failed to update backup error state in settings.");

        }

    }



    public async Task<BackupVerifyResultDto> VerifyBackupAsync(int backupRecordId, string? password = null, CancellationToken cancellationToken = default)

    {

        var record = await _dbContext.BackupRecords.FirstOrDefaultAsync(b => b.Id == backupRecordId, cancellationToken);

        if (record == null)

        {

            return new BackupVerifyResultDto

            {

                IsValid = false,

                Status = BackupVerifyStatus.Failed,

                Message = $"Backup record with ID {backupRecordId} not found."

            };

        }



        return await VerifyBackupFileInternalAsync(record.FilePath, password, record.Id, cancellationToken);

    }



    public async Task<BackupVerifyResultDto> VerifyBackupFileAsync(string filePath, string? password = null, CancellationToken cancellationToken = default)

    {

        return await VerifyBackupFileInternalAsync(filePath, password, null, cancellationToken);

    }



    private async Task<BackupVerifyResultDto> VerifyBackupFileInternalAsync(string filePath, string? password, int? recordId, CancellationToken cancellationToken)

    {

        if (!File.Exists(filePath))

        {

            await UpdateRecordVerificationAsync(recordId, BackupVerifyStatus.Failed, "Backup file missing on disk.", cancellationToken);

            return new BackupVerifyResultDto

            {

                IsValid = false,

                Status = BackupVerifyStatus.Failed,

                Message = $"File not found: {filePath}"

            };

        }



        string tempDir = Path.Combine(_appPaths.TempDirectory, $"verify_{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDir);



        try

        {

            string zipPath = filePath;

            bool isEncrypted = _cryptoService.IsEncryptedBackup(filePath);



            if (isEncrypted)

            {

                if (string.IsNullOrWhiteSpace(password))

                {

                    return new BackupVerifyResultDto

                    {

                        IsValid = false,

                        Status = BackupVerifyStatus.Failed,

                        Message = "Encrypted backup requires a password for verification."

                    };

                }



                zipPath = Path.Combine(tempDir, "decrypted.zip");

                _cryptoService.DecryptFile(filePath, zipPath, password);

            }



            // Test opening zip archive

            using var zipArchive = ZipFile.OpenRead(zipPath);

            var manifestEntry = zipArchive.GetEntry("manifest.json");

            var dbEntry = zipArchive.GetEntry("shop.db");



            if (dbEntry == null)

            {

                await UpdateRecordVerificationAsync(recordId, BackupVerifyStatus.Failed, "Backup zip is missing shop.db.", cancellationToken);

                return new BackupVerifyResultDto

                {

                    IsValid = false,

                    Status = BackupVerifyStatus.Failed,

                    Message = "Archive does not contain 'shop.db'."

                };

            }



            BackupManifest? manifest = null;

            if (manifestEntry != null)

            {

                using var stream = manifestEntry.Open();

                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, cancellationToken: cancellationToken);

            }



            // Extract shop.db to temp and verify checksum

            string extractedDb = Path.Combine(tempDir, "shop.db");

            dbEntry.ExtractToFile(extractedDb, overwrite: true);



            string extractedSha256;

            using (var sha = SHA256.Create())

            using (var stream = File.OpenRead(extractedDb))

            {

                extractedSha256 = Convert.ToHexString(await sha.ComputeHashAsync(stream, cancellationToken));

            }



            if (manifest != null && !string.IsNullOrWhiteSpace(manifest.DatabaseSha256))

            {

                if (!string.Equals(manifest.DatabaseSha256, extractedSha256, StringComparison.OrdinalIgnoreCase))

                {

                    await UpdateRecordVerificationAsync(recordId, BackupVerifyStatus.Failed, "Database SHA-256 hash mismatch.", cancellationToken);

                    return new BackupVerifyResultDto

                    {

                        IsValid = false,

                        Status = BackupVerifyStatus.Failed,

                        Message = $"Database checksum mismatch. Expected {manifest.DatabaseSha256}, got {extractedSha256}."

                    };

                }

            }



            // Run PRAGMA integrity_check on extracted DB

            using (var testConn = new SqliteConnection($"Data Source={extractedDb};Mode=ReadOnly;Pooling=False"))

            {

                await testConn.OpenAsync(cancellationToken);

                using var cmd = testConn.CreateCommand();

                cmd.CommandText = "PRAGMA integrity_check;";

                var res = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();



                if (!string.Equals(res, "ok", StringComparison.OrdinalIgnoreCase))

                {

                    await UpdateRecordVerificationAsync(recordId, BackupVerifyStatus.Failed, $"SQLite integrity_check failed: {res}", cancellationToken);

                    return new BackupVerifyResultDto

                    {

                        IsValid = false,

                        Status = BackupVerifyStatus.Failed,

                        Message = $"SQLite integrity_check returned: {res}"

                    };

                }

            }



            await UpdateRecordVerificationAsync(recordId, BackupVerifyStatus.Ok, null, cancellationToken);

            return new BackupVerifyResultDto

            {

                IsValid = true,

                Status = BackupVerifyStatus.Ok,

                Message = "Backup passed all integrity and checksum checks.",

                RowCounts = manifest?.TableRowCounts ?? new Dictionary<string, int>(),

                SchemaVersion = manifest?.SchemaVersion

            };

        }

        catch (Exception ex)

        {

            _logger.LogWarning(ex, "Verification failed for backup {FilePath}", filePath);

            await UpdateRecordVerificationAsync(recordId, BackupVerifyStatus.Failed, ex.Message, cancellationToken);

            return new BackupVerifyResultDto

            {

                IsValid = false,

                Status = BackupVerifyStatus.Failed,

                Message = ex.Message

            };

        }

        finally

        {

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();



            try { Directory.Delete(tempDir, recursive: true); } catch { }

        }

    }



    private async Task UpdateRecordVerificationAsync(int? recordId, BackupVerifyStatus status, string? error, CancellationToken cancellationToken)

    {

        if (recordId == null) return;

        try

        {

            var record = await _dbContext.BackupRecords.FirstOrDefaultAsync(b => b.Id == recordId.Value, cancellationToken);

            if (record != null)

            {

                record.VerifyStatus = status;

                record.VerifiedAt = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(error))

                {

                    record.ErrorMessage = error;

                }

                await _dbContext.SaveChangesAsync(cancellationToken);

            }

        }

        catch (Exception ex)

        {

            _logger.LogError(ex, "Failed to update verification status for backup record {Id}", recordId);

        }

    }



    public async Task<IReadOnlyList<BackupRecordDto>> GetBackupsAsync(bool includeDeleted = false, CancellationToken cancellationToken = default)

    {

        var query = _dbContext.BackupRecords.AsNoTracking();

        if (!includeDeleted)

        {

            query = query.Where(b => !b.IsDeletedLocally);

        }



        var list = await query

            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)

            .ToListAsync(cancellationToken);



        return list.Select(MapToDto).ToList();

    }



    public async Task<BackupSummaryDto> GetBackupSummaryAsync(CancellationToken cancellationToken = default)

    {

        var settings = await _dbContext.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new ShopSetting();

        var latestBackup = await _dbContext.BackupRecords

            .AsNoTracking()

            .Where(b => b.Status == BackupStatus.Created && !b.IsDeletedLocally)

            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)

            .FirstOrDefaultAsync(cancellationToken);



        long freeSpace = 0;

        try

        {

            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(_appPaths.BackupsDirectory))!);

            freeSpace = drive.AvailableFreeSpace;

        }

        catch { }



        // Calculate next scheduled backup

        DateTime? nextScheduled = null;

        if (settings.BackupEnabled && TimeSpan.TryParse(settings.BackupTime, out var timeOfDay))

        {

            var now = DateTime.Now;

            var candidate = now.Date.Add(timeOfDay);

            if (candidate <= now) candidate = candidate.AddDays(1);

            nextScheduled = candidate;

        }



        // Warnings check: no backup in 2 days or last backup failed

        bool hasWarning = false;

        string? warningMessage = null;



        if (!string.IsNullOrWhiteSpace(settings.LastBackupError))

        {

            hasWarning = true;

            warningMessage = $"Last backup attempt failed: {settings.LastBackupError}";

        }

        else if (settings.LastSuccessfulBackupAt.HasValue)

        {

            if (DateTime.UtcNow - settings.LastSuccessfulBackupAt.Value > TimeSpan.FromDays(2))

            {

                hasWarning = true;

                warningMessage = "No backup has been taken in the last 2 days. It is strongly recommended to back up your data.";

            }

        }

        else

        {

            hasWarning = true;

            warningMessage = "No backup has been created yet. Back up your database now to prevent data loss.";

        }



        int totalBackups = await _dbContext.BackupRecords.CountAsync(b => !b.IsDeletedLocally, cancellationToken);



        bool isDriveConfigured = false;

        bool isDriveConnected = false;

        string? driveAccountEmail = settings.GoogleDriveAccountEmail;

        int drivePendingCount = 0;

        bool isWaitingForInternet = false;

        bool isDriveReconnect = settings.GoogleDriveReconnectNeeded;

        bool isDriveFull = settings.GoogleDriveIsFull;

        string? driveFolderUrl = null;

        DateTime? lastDriveUpload = settings.LastSuccessfulDriveUploadAt;



        if (_backupUploader != null)

        {

            try

            {

                var status = await _backupUploader.GetStatusSummaryAsync(cancellationToken);

                isDriveConfigured = status.IsConfiguredInBuild;

                isDriveConnected = status.IsConnected;

                driveAccountEmail = status.AccountEmail ?? driveAccountEmail;

                drivePendingCount = status.PendingUploadCount;

                isWaitingForInternet = status.IsWaitingForInternet;

                isDriveReconnect = status.ReconnectNeeded;

                isDriveFull = status.IsDriveFull;

                driveFolderUrl = status.FolderWebUrl;

                lastDriveUpload = status.LastUploadAt ?? lastDriveUpload;

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Failed to retrieve Google Drive status summary.");

            }

        }



        string driveStatusText = isDriveConnected

            ? (driveAccountEmail ?? "Connected")

            : (isDriveConfigured ? "Not connected" : "Not configured in build");



        return new BackupSummaryDto

        {

            LastBackupAt = latestBackup?.CreatedAt ?? settings.LastSuccessfulBackupAt,

            LastBackupResult = latestBackup != null ? "Success" : (settings.LastBackupError ?? "None"),

            LastBackupSucceeded = string.IsNullOrWhiteSpace(settings.LastBackupError),

            LastBackupFileName = latestBackup?.FileName,

            NextScheduledBackupAt = nextScheduled,

            BackupDirectory = _appPaths.BackupsDirectory,

            FreeDiskSpaceBytes = freeSpace,

            DriveStatusText = driveStatusText,

            IsDriveConnected = isDriveConnected,

            DriveAccountEmail = driveAccountEmail,

            DrivePendingCount = drivePendingCount,

            IsWaitingForInternet = isWaitingForInternet,

            IsDriveReconnectNeeded = isDriveReconnect,

            IsDriveFull = isDriveFull,

            IsDriveConfiguredInBuild = isDriveConfigured,
            DriveFolderUrl = driveFolderUrl,
            LastDriveUploadAt = lastDriveUpload,
            IsGitHubConnected = settings.GitHubEnabled && !string.IsNullOrWhiteSpace(settings.GitHubOwner) && !string.IsNullOrWhiteSpace(settings.GitHubRepo),
            GitHubRepoFullName = !string.IsNullOrWhiteSpace(settings.GitHubOwner) ? $"{settings.GitHubOwner}/{settings.GitHubRepo}" : null,
            GitHubPendingCount = await _dbContext.BackupRecords.CountAsync(b => !b.IsDeleted && b.Status == BackupStatus.Created && (b.GitHubStatus == GitHubUploadStatus.Pending || b.GitHubStatus == GitHubUploadStatus.NotConfigured || b.GitHubStatus == GitHubUploadStatus.Failed), cancellationToken),
            IsGitHubReconnectNeeded = settings.GitHubReconnectNeeded,
            IsGitHubRepoPublic = settings.GitHubRepoIsPublic,
            IsGitHubFileTooLarge = settings.GitHubFileTooLarge,
            LastGitHubUploadAt = settings.LastSuccessfulGitHubUploadAt,
            HasWarning = hasWarning,
            WarningMessage = warningMessage,
            TotalBackupsCount = totalBackups
        };

    }



    public async Task<BackupSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)

    {

        var s = await _dbContext.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new ShopSetting();

        bool isConfigured = false;

        if (_backupUploader != null)

        {

            try

            {

                var status = await _backupUploader.GetStatusSummaryAsync(cancellationToken);

                isConfigured = status.IsConfiguredInBuild;

            }

            catch { }

        }



        return new BackupSettingsDto

        {

            BackupEnabled = s.BackupEnabled,

            BackupTime = s.BackupTime,

            BackupOnAppClose = s.BackupOnAppClose,

            KeepDaily = s.KeepDaily,

            KeepWeekly = s.KeepWeekly,

            KeepMonthly = s.KeepMonthly,

            SecondFolderPath = s.SecondFolderPath,

            EncryptBackups = s.EncryptBackups,

            HasPasswordSet = !string.IsNullOrWhiteSpace(s.EncryptionKeyCheck),

            LastSuccessfulBackupAt = s.LastSuccessfulBackupAt,

            LastBackupError = s.LastBackupError,

            GoogleDriveEnabled = s.GoogleDriveEnabled,

            GoogleDriveAccountEmail = s.GoogleDriveAccountEmail,

            GoogleDriveFolderId = s.GoogleDriveFolderId,

            GoogleDriveSubFolderId = s.GoogleDriveSubFolderId,

            KeepManualBackupsOnDrive = s.KeepManualBackupsOnDrive,

            GoogleDriveDeleteToTrash = s.GoogleDriveDeleteToTrash,

            LastSuccessfulDriveUploadAt = s.LastSuccessfulDriveUploadAt,

            LastDriveUploadError = s.LastDriveUploadError,

            GoogleDriveReconnectNeeded = s.GoogleDriveReconnectNeeded,
            GoogleDriveIsFull = s.GoogleDriveIsFull,
            IsDriveConfiguredInBuild = isConfigured,
            GitHubEnabled = s.GitHubEnabled,
            GitHubOwner = s.GitHubOwner,
            GitHubRepo = s.GitHubRepo,
            GitHubBranch = s.GitHubBranch ?? "main",
            GitHubExcludeImages = s.GitHubExcludeImages,
            LastSuccessfulGitHubUploadAt = s.LastSuccessfulGitHubUploadAt,
            LastGitHubUploadError = s.LastGitHubUploadError,
            GitHubReconnectNeeded = s.GitHubReconnectNeeded,
            GitHubRepoIsPublic = s.GitHubRepoIsPublic,
            GitHubFileTooLarge = s.GitHubFileTooLarge
        };
    }

    public async Task UpdateSettingsAsync(BackupSettingsDto dto, CancellationToken cancellationToken = default)
    {
        var s = await _dbContext.ShopSettings.FirstOrDefaultAsync(cancellationToken);
        if (s == null)
        {
            s = new ShopSetting();
            _dbContext.ShopSettings.Add(s);
        }

        s.BackupEnabled = dto.BackupEnabled;
        s.BackupTime = dto.BackupTime;
        s.BackupOnAppClose = dto.BackupOnAppClose;
        s.KeepDaily = Math.Max(1, dto.KeepDaily);
        s.KeepWeekly = Math.Max(1, dto.KeepWeekly);
        s.KeepMonthly = Math.Max(1, dto.KeepMonthly);
        s.SecondFolderPath = string.IsNullOrWhiteSpace(dto.SecondFolderPath) ? null : dto.SecondFolderPath.Trim();
        s.EncryptBackups = dto.EncryptBackups;
        s.GoogleDriveEnabled = dto.GoogleDriveEnabled;
        s.KeepManualBackupsOnDrive = dto.KeepManualBackupsOnDrive;
        s.GoogleDriveDeleteToTrash = dto.GoogleDriveDeleteToTrash;
        s.GitHubEnabled = dto.GitHubEnabled;
        s.GitHubOwner = dto.GitHubOwner?.Trim();
        s.GitHubRepo = dto.GitHubRepo?.Trim();
        s.GitHubBranch = string.IsNullOrWhiteSpace(dto.GitHubBranch) ? "main" : dto.GitHubBranch.Trim();
        s.GitHubExcludeImages = dto.GitHubExcludeImages;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }



    public async Task SetEncryptionPasswordAsync(string password, CancellationToken cancellationToken = default)

    {

        if (string.IsNullOrWhiteSpace(password))

        {

            throw new ArgumentException("Password cannot be empty.", nameof(password));

        }



        var s = await _dbContext.ShopSettings.FirstOrDefaultAsync(cancellationToken);

        if (s == null)

        {

            s = new ShopSetting();

            _dbContext.ShopSettings.Add(s);

        }



        byte[] salt = new byte[BackupCryptoService.SaltSizeBytes];

        RandomNumberGenerator.Fill(salt);



        byte[] key = _cryptoService.DeriveKey(password, salt);

        string keyCheck = _cryptoService.ComputeKeyCheck(key);



        s.EncryptionSalt = Convert.ToBase64String(salt);

        s.EncryptionKeyCheck = keyCheck;

        s.EncryptBackups = true;



        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Backup encryption password has been set and encryption enabled.");

    }



    public async Task DisableEncryptionAsync(string currentPassword, CancellationToken cancellationToken = default)

    {

        var s = await _dbContext.ShopSettings.FirstOrDefaultAsync(cancellationToken);

        if (s == null || string.IsNullOrWhiteSpace(s.EncryptionKeyCheck))

        {

            if (s != null) s.EncryptBackups = false;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return;

        }



        if (!_cryptoService.ValidateKeyCheck(currentPassword, s.EncryptionSalt ?? string.Empty, s.EncryptionKeyCheck))

        {

            throw new InvalidOperationException("Current password is incorrect.");

        }



        s.EncryptBackups = false;

        s.EncryptionSalt = null;

        s.EncryptionKeyCheck = null;



        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Backup encryption has been disabled.");

    }



    public async Task<bool> ValidatePasswordAsync(string password, CancellationToken cancellationToken = default)

    {

        var s = await _dbContext.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (s == null || string.IsNullOrWhiteSpace(s.EncryptionKeyCheck) || string.IsNullOrWhiteSpace(s.EncryptionSalt))

        {

            return false;

        }



        return _cryptoService.ValidateKeyCheck(password, s.EncryptionSalt, s.EncryptionKeyCheck);

    }



    public async Task<int> RunRetentionAsync(CancellationToken cancellationToken = default)

    {

        var settings = await _dbContext.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new ShopSetting();

        int keepDaily = Math.Max(1, settings.KeepDaily);

        int keepWeekly = Math.Max(1, settings.KeepWeekly);

        int keepMonthly = Math.Max(1, settings.KeepMonthly);



        var allBackups = await _dbContext.BackupRecords

            .Where(b => !b.IsDeletedLocally)

            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)

            .ToListAsync(cancellationToken);



        if (allBackups.Count == 0) return 0;



        var newestBackup = allBackups.First();

        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);



        // Retention candidates:

        // Exclude manual backups (kept until user deletes them)

        // Exclude young PreRestore backups (< 30 days)

        // Exclude pending or uploading Drive sync

        // Exclude the newest backup

        var keepSet = new HashSet<int> { newestBackup.Id };



        var candidates = allBackups

            .Where(b => b.Id != newestBackup.Id)

            .Where(b => b.Kind != BackupKind.Manual)

            .Where(b => !(b.Kind == BackupKind.PreRestore && b.CreatedAt >= thirtyDaysAgo))

            .Where(b => b.DriveStatus != DriveUploadStatus.Pending && b.DriveStatus != DriveUploadStatus.Uploading)

            .ToList();



        // 1. Keep newest daily backups (KeepDaily distinct calendar days)

        var dailyKeep = candidates

            .GroupBy(b => b.CreatedAt.Date)

            .OrderByDescending(g => g.Key)

            .Take(keepDaily)

            .Select(g => g.OrderByDescending(b => b.CreatedAt).First().Id);



        foreach (var id in dailyKeep) keepSet.Add(id);



        // 2. Keep newest weekly backups (KeepWeekly distinct weeks)

        var weeklyKeep = candidates

            .GroupBy(b => GetCalendarWeekKey(b.CreatedAt))

            .OrderByDescending(g => g.Key)

            .Take(keepWeekly)

            .Select(g => g.OrderByDescending(b => b.CreatedAt).First().Id);



        foreach (var id in weeklyKeep) keepSet.Add(id);



        // 3. Keep newest monthly backups (KeepMonthly distinct months)

        var monthlyKeep = candidates

            .GroupBy(b => new DateTime(b.CreatedAt.Year, b.CreatedAt.Month, 1))

            .OrderByDescending(g => g.Key)

            .Take(keepMonthly)

            .Select(g => g.OrderByDescending(b => b.CreatedAt).First().Id);



        foreach (var id in monthlyKeep) keepSet.Add(id);



        // Any candidate not in keepSet should be deleted

        var toDelete = candidates.Where(b => !keepSet.Contains(b.Id)).ToList();

        int deletedCount = 0;



        foreach (var rec in toDelete)

        {

            try

            {

                if (File.Exists(rec.FilePath))

                {

                    File.Delete(rec.FilePath);

                }



                rec.IsDeletedLocally = true;

                deletedCount++;

                _logger.LogInformation("Retention deleted old backup archive: {FileName}", rec.FileName);

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Could not delete backup file {FilePath} during retention", rec.FilePath);

            }

        }



        if (deletedCount > 0)

        {

            await _dbContext.SaveChangesAsync(cancellationToken);

        }



        return deletedCount;

    }



    private static string GetCalendarWeekKey(DateTime dt)

    {

        // ISO calendar week or year-week

        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;

        int week = cal.GetWeekOfYear(dt, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

        return $"{dt.Year}-W{week:D2}";

    }



    public async Task<bool> DeleteBackupAsync(int backupRecordId, CancellationToken cancellationToken = default)

    {

        var res = await DeleteBackupsAsync(new[] { backupRecordId }, cancellationToken);

        return res.DeletedCount > 0;

    }



    public async Task<(int DeletedCount, int BlockedCount, string Summary)> DeleteBackupsAsync(IEnumerable<int> backupRecordIds, CancellationToken cancellationToken = default)

    {

        var ids = backupRecordIds.Distinct().ToList();

        if (ids.Count == 0) return (0, 0, "No backups selected.");



        var allRecords = await _dbContext.BackupRecords

            .Where(b => !b.IsDeletedLocally)

            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)

            .ToListAsync(cancellationToken);



        var newestBackup = allRecords.FirstOrDefault();

        int deleted = 0;

        int blocked = 0;

        var blockedReasons = new List<string>();



        foreach (var id in ids)

        {

            var record = allRecords.FirstOrDefault(b => b.Id == id);

            if (record == null) continue;



            // Block deleting newest backup

            if (newestBackup != null && record.Id == newestBackup.Id)

            {

                blocked++;

                blockedReasons.Add($"'{record.FileName}' is the newest backup.");

                continue;

            }



            // Block deleting pending or active cloud upload

            if (record.DriveStatus == DriveUploadStatus.Pending || record.DriveStatus == DriveUploadStatus.Uploading)

            {

                blocked++;

                blockedReasons.Add($"'{record.FileName}' has cloud upload in progress.");

                continue;

            }



            try

            {

                if (File.Exists(record.FilePath))

                {

                    File.Delete(record.FilePath);

                }



                record.IsDeletedLocally = true;

                deleted++;

                _logger.LogInformation("Deleted backup: {FileName}", record.FileName);

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Could not delete backup file: {FilePath}", record.FilePath);

                blocked++;

                blockedReasons.Add($"Could not delete file '{record.FileName}': {ex.Message}");

            }

        }



        if (deleted > 0)

        {

            await _dbContext.SaveChangesAsync(cancellationToken);

        }



        string summary = deleted > 0

            ? $"Deleted {deleted} backup{(deleted == 1 ? "" : "s")}."

            : "No backups deleted.";



        if (blocked > 0)

        {

            summary += $" {blocked} backup{(blocked == 1 ? " was" : "s were")} blocked: {string.Join(" ", blockedReasons.Take(2))}";

        }



        return (deleted, blocked, summary);

    }



    public async Task RegisterExistingBackupsAsync(CancellationToken cancellationToken = default)

    {

        if (!Directory.Exists(_appPaths.BackupsDirectory)) return;



        var existingFilePaths = await _dbContext.BackupRecords

            .AsNoTracking()

            .Select(b => b.FilePath)

            .ToListAsync(cancellationToken);



        var knownFiles = new HashSet<string>(existingFilePaths, StringComparer.OrdinalIgnoreCase);



        var files = Directory.GetFiles(_appPaths.BackupsDirectory, "*.*")

            .Where(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||

                        f.EndsWith(".enc", StringComparison.OrdinalIgnoreCase) ||

                        f.EndsWith(".db", StringComparison.OrdinalIgnoreCase))

            .ToList();



        int registered = 0;

        foreach (var file in files)

        {

            if (knownFiles.Contains(file)) continue;



            try

            {

                var fi = new FileInfo(file);

                string name = fi.Name;

                BackupKind kind = BackupKind.Manual;



                if (name.StartsWith("pre-migration", StringComparison.OrdinalIgnoreCase))

                    kind = BackupKind.PreMigration;

                else if (name.StartsWith("backup-clear", StringComparison.OrdinalIgnoreCase) ||

                         name.StartsWith("backup-load", StringComparison.OrdinalIgnoreCase))

                    kind = BackupKind.PreDataTool;

                else if (name.Contains("scheduled", StringComparison.OrdinalIgnoreCase))

                    kind = BackupKind.Scheduled;

                else if (name.Contains("prerestore", StringComparison.OrdinalIgnoreCase))

                    kind = BackupKind.PreRestore;



                string sha256;

                using (var sha = SHA256.Create())

                using (var fs = File.OpenRead(file))

                {

                    sha256 = Convert.ToHexString(await sha.ComputeHashAsync(fs, cancellationToken));

                }



                bool isEncrypted = _cryptoService.IsEncryptedBackup(file);



                var record = new BackupRecord

                {

                    FileName = name,

                    FilePath = file,

                    SizeBytes = fi.Length,

                    Sha256 = sha256,

                    Kind = kind,

                    IsEncrypted = isEncrypted,

                    SchemaVersion = "Legacy",

                    AppVersion = "1.0.0",

                    VerifyStatus = BackupVerifyStatus.NotVerified,

                    Status = BackupStatus.Created,

                    DriveStatus = DriveUploadStatus.NotConfigured,

                    IsDeletedLocally = false

                };



                _dbContext.BackupRecords.Add(record);

                registered++;

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Failed to register legacy backup file: {File}", file);

            }

        }



        if (registered > 0)

        {

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Discovered and registered {Count} legacy backup files.", registered);

        }

    }



    public async Task CopyBackupToAsync(int backupRecordId, string destinationFilePath, CancellationToken cancellationToken = default)

    {

        var record = await _dbContext.BackupRecords.AsNoTracking().FirstOrDefaultAsync(b => b.Id == backupRecordId, cancellationToken);

        if (record == null)

        {

            throw new FileNotFoundException($"Backup record {backupRecordId} not found.");

        }



        if (!File.Exists(record.FilePath))

        {

            throw new FileNotFoundException($"Backup file not found at: {record.FilePath}");

        }



        string? dir = Path.GetDirectoryName(destinationFilePath);

        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))

        {

            Directory.CreateDirectory(dir);

        }



        File.Copy(record.FilePath, destinationFilePath, overwrite: true);

        _logger.LogInformation("Backup {Id} copied to {Dest}", backupRecordId, destinationFilePath);

    }



    private static BackupRecordDto MapToDto(BackupRecord b)

    {

        return new BackupRecordDto

        {

            Id = b.Id,

            FileName = b.FileName,

            FilePath = b.FilePath,

            CreatedAt = b.CreatedAt,

            SizeBytes = b.SizeBytes,

            Sha256 = b.Sha256,

            Kind = b.Kind,

            IsEncrypted = b.IsEncrypted,

            SchemaVersion = b.SchemaVersion,

            AppVersion = b.AppVersion,

            VerifyStatus = b.VerifyStatus,

            VerifiedAt = b.VerifiedAt,

            Status = b.Status,

            ErrorMessage = b.ErrorMessage,

            DriveStatus = b.DriveStatus,

            DriveFileId = b.DriveFileId,

            DriveUploadedAt = b.DriveUploadedAt,

            DriveAttempts = b.DriveAttempts,

            DriveLastError = b.DriveLastError,
            DriveNextAttemptAt = b.DriveNextAttemptAt,
            GitHubStatus = b.GitHubStatus,
            GitHubFileSha = b.GitHubFileSha,
            GitHubUploadedAt = b.GitHubUploadedAt,
            GitHubAttempts = b.GitHubAttempts,
            GitHubLastError = b.GitHubLastError,
            GitHubNextAttemptAt = b.GitHubNextAttemptAt,
            IsDeletedLocally = b.IsDeletedLocally
        };

    }



    private static string SanitizeFileName(string name)

    {

        var invalid = Path.GetInvalidFileNameChars();

        var chars = name.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();

        return new string(chars);

    }

}

