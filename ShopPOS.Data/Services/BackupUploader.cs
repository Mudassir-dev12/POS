using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Orchestrates background Google Drive backup uploads, resumable uploads, exponential backoff,
/// MD5 verification, Drive retention policy, and downloading archives for restore.
/// </summary>
public class BackupUploader : IBackupUploader
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly AppDbContext? _dbContext;
    private readonly IDriveClient _driveClient;
    private readonly IDriveTokenStore _tokenStore;
    private readonly IGoogleDriveConfigService _configService;
    private readonly IAppPaths _appPaths;
    private readonly ILogger<BackupUploader> _logger;

    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private bool _isWaitingForInternet;

    public bool IsQueueBusy => _queueLock.CurrentCount == 0;

    [ActivatorUtilitiesConstructor]
    public BackupUploader(
        IServiceScopeFactory scopeFactory,
        IDriveClient driveClient,
        IDriveTokenStore tokenStore,
        IGoogleDriveConfigService configService,
        IAppPaths appPaths,
        ILogger<BackupUploader>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _driveClient = driveClient;
        _tokenStore = tokenStore;
        _configService = configService;
        _appPaths = appPaths;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupUploader>.Instance;
    }

    internal BackupUploader(
        AppDbContext dbContext,
        IDriveClient driveClient,
        IDriveTokenStore tokenStore,
        IGoogleDriveConfigService configService,
        IAppPaths appPaths,
        ILogger<BackupUploader>? logger = null)
    {
        _dbContext = dbContext;
        _driveClient = driveClient;
        _tokenStore = tokenStore;
        _configService = configService;
        _appPaths = appPaths;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupUploader>.Instance;
    }

    private readonly SemaphoreSlim _dbLock = new(1, 1);

    private async Task<T> WithDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        if (_dbContext != null)
        {
            await _dbLock.WaitAsync();
            try
            {
                return await action(_dbContext);
            }
            finally
            {
                _dbLock.Release();
            }
        }

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await action(db);
        }

        throw new InvalidOperationException("No database context or scope factory configured for BackupUploader.");
    }

    private async Task WithDbContextAsync(Func<AppDbContext, Task> action)
    {
        if (_dbContext != null)
        {
            await _dbLock.WaitAsync();
            try
            {
                await action(_dbContext);
                return;
            }
            finally
            {
                _dbLock.Release();
            }
        }

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await action(db);
            return;
        }

        throw new InvalidOperationException("No database context or scope factory configured for BackupUploader.");
    }

    public async Task<bool> ConnectAsync(Func<string, Task>? onBrowserOpen = null, CancellationToken cancellationToken = default)
    {
        if (!_configService.IsConfigured())
        {
            throw new InvalidOperationException("Google Drive is not configured in this build.");
        }

        string email = await _driveClient.AuthorizeAsync(onBrowserOpen ?? (_ => Task.CompletedTask), cancellationToken);

        if (!_tokenStore.HasToken())
        {
            await _tokenStore.SaveTokenAsync("{\"access_token\":\"connected\"}", cancellationToken);
        }

        // Find or create root folder "ShopPOS Backups"
        string rootFolderId = await _driveClient.GetOrCreateFolderAsync("ShopPOS Backups", null, cancellationToken);

        // Find or create shop subfolder
        string shopName = await WithDbContextAsync(async db =>
        {
            var setting = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
            return setting?.ShopName ?? "ShopPOS";
        });

        string shopSlug = SanitizeSlug(shopName);
        string subFolderId = await _driveClient.GetOrCreateFolderAsync(shopSlug, rootFolderId, cancellationToken);

        // Update settings in database
        await WithDbContextAsync(async db =>
        {
            var setting = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
            if (setting == null)
            {
                setting = new ShopSetting();
                db.ShopSettings.Add(setting);
            }

            setting.GoogleDriveEnabled = true;
            setting.GoogleDriveAccountEmail = email;
            setting.GoogleDriveFolderId = rootFolderId;
            setting.GoogleDriveSubFolderId = subFolderId;
            setting.GoogleDriveReconnectNeeded = false;
            setting.GoogleDriveIsFull = false;
            setting.LastDriveUploadError = null;

            await db.SaveChangesAsync(cancellationToken);
        });

        _logger.LogInformation("Successfully connected Google Drive account '{Email}'. Root folder: {Root}, Subfolder: {Sub}", email, rootFolderId, subFolderId);

        // Process any queued backups immediately
        try
        {
            await ProcessQueueAsync(force: true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial backup queue processing after connect failed.");
        }

        return true;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _driveClient.RevokeTokenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to revoke token online during disconnect.");
        }

        await _tokenStore.DeleteTokenAsync(cancellationToken);

        await WithDbContextAsync(async db =>
        {
            var setting = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
            if (setting != null)
            {
                setting.GoogleDriveEnabled = false;
                setting.GoogleDriveAccountEmail = null;
                setting.GoogleDriveFolderId = null;
                setting.GoogleDriveSubFolderId = null;
                setting.GoogleDriveReconnectNeeded = false;
                setting.GoogleDriveIsFull = false;
                setting.LastDriveUploadError = null;

                await db.SaveChangesAsync(cancellationToken);
            }
        });

        _logger.LogInformation("Disconnected Google Drive and cleared local credentials.");
    }

    public async Task<DriveStatusSummaryDto> GetStatusSummaryAsync(CancellationToken cancellationToken = default)
    {
        bool isConfigured = _configService.IsConfigured();
        bool hasToken = _tokenStore.HasToken();

        return await WithDbContextAsync(async db =>
        {
            var setting = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new ShopSetting();
            int pendingCount = await db.BackupRecords.CountAsync(r =>
                (r.DriveStatus == DriveUploadStatus.Pending || r.DriveStatus == DriveUploadStatus.Failed) &&
                !r.IsDeletedLocally, cancellationToken);

            return new DriveStatusSummaryDto
            {
                IsConfiguredInBuild = isConfigured,
                IsConnected = hasToken,
                AccountEmail = setting.GoogleDriveAccountEmail,
                IsDriveEnabled = setting.GoogleDriveEnabled,
                PendingUploadCount = pendingCount,
                IsWaitingForInternet = _isWaitingForInternet,
                ReconnectNeeded = setting.GoogleDriveReconnectNeeded,
                IsDriveFull = setting.GoogleDriveIsFull,
                LastUploadAt = setting.LastSuccessfulDriveUploadAt,
                LastError = setting.LastDriveUploadError,
                FolderId = setting.GoogleDriveFolderId,
                SubFolderId = setting.GoogleDriveSubFolderId
            };
        });
    }

    public async Task ProcessQueueAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        var lockTimeout = force ? TimeSpan.FromSeconds(10) : TimeSpan.Zero;
        if (!await _queueLock.WaitAsync(lockTimeout, cancellationToken))
        {
            _logger.LogDebug("Backup queue processor already running. Skipping trigger.");
            return;
        }

        try
        {
            if (!_configService.IsConfigured() || !_tokenStore.HasToken())
            {
                return;
            }

            var setting = await WithDbContextAsync(async db =>
                await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new ShopSetting());

            if (!setting.GoogleDriveEnabled && !force)
            {
                return;
            }

            if ((setting.GoogleDriveReconnectNeeded || setting.GoogleDriveIsFull) && !force)
            {
                _logger.LogDebug("Google Drive queue paused due to reconnect needed or quota full.");
                return;
            }

            // Check internet connectivity
            bool isOnline = await CheckInternetAccessAsync(cancellationToken);
            if (!isOnline)
            {
                _isWaitingForInternet = true;
                _logger.LogInformation("No internet connection detected; Google Drive backup queue waiting for network.");
                return;
            }

            _isWaitingForInternet = false;

            // Fetch pending or failed backups ordered by CreatedAt (oldest first)
            var eligibleRecords = await WithDbContextAsync(async db =>
            {
                var query = db.BackupRecords
                    .Where(r => (r.DriveStatus == DriveUploadStatus.Pending || r.DriveStatus == DriveUploadStatus.Failed) && !r.IsDeletedLocally);

                if (!force)
                {
                    var now = DateTime.UtcNow;
                    query = query.Where(r => r.DriveNextAttemptAt == null || r.DriveNextAttemptAt <= now);
                }

                return await query.OrderBy(r => r.CreatedAt).Select(r => r.Id).ToListAsync(cancellationToken);
            });

            if (eligibleRecords.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Processing {Count} pending Google Drive backup uploads.", eligibleRecords.Count);

            foreach (int recordId in eligibleRecords)
            {
                if (cancellationToken.IsCancellationRequested) break;

                bool continueNext = await UploadRecordInternalAsync(recordId, force, cancellationToken);
                if (!continueNext)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Google Drive upload queue loop.");
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task<bool> UploadSingleBackupAsync(int backupRecordId, CancellationToken cancellationToken = default)
    {
        return await UploadRecordInternalAsync(backupRecordId, force: true, cancellationToken);
    }

    private async Task<bool> UploadRecordInternalAsync(int recordId, bool force, CancellationToken cancellationToken)
    {
        return await WithDbContextAsync(async db =>
        {
            var record = await db.BackupRecords.FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);
            if (record == null || record.IsDeletedLocally) return true;

            var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken) ?? new ShopSetting();

            if (!File.Exists(record.FilePath))
            {
                record.DriveStatus = DriveUploadStatus.Failed;
                record.DriveLastError = $"Local backup file '{record.FilePath}' does not exist.";
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }

            // Ensure destination folder exists
            string? subFolderId = settings.GoogleDriveSubFolderId;
            if (string.IsNullOrWhiteSpace(subFolderId))
            {
                string rootFolderId = settings.GoogleDriveFolderId ??
                    await _driveClient.GetOrCreateFolderAsync("ShopPOS Backups", null, cancellationToken);
                settings.GoogleDriveFolderId = rootFolderId;

                string shopName = string.IsNullOrWhiteSpace(settings.ShopName) ? "ShopPOS" : settings.ShopName;
                subFolderId = await _driveClient.GetOrCreateFolderAsync(SanitizeSlug(shopName), rootFolderId, cancellationToken);
                settings.GoogleDriveSubFolderId = subFolderId;
                await db.SaveChangesAsync(cancellationToken);
            }

            // Calculate local MD5 checksum if not already saved
            string localMd5 = record.DriveMd5Checksum ?? string.Empty;
            if (string.IsNullOrWhiteSpace(localMd5))
            {
                localMd5 = await ComputeFileMd5Async(record.FilePath, cancellationToken);
                record.DriveMd5Checksum = localMd5;
            }

            record.DriveStatus = DriveUploadStatus.Uploading;
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Uploading backup '{FileName}' to Google Drive (Attempt {Attempt})", record.FileName, record.DriveAttempts + 1);

            var uploadResult = await _driveClient.UploadFileResumableAsync(
                record.FilePath,
                subFolderId,
                record.DriveUploadSessionUri,
                null,
                cancellationToken);

            if (uploadResult.Success)
            {
                // Verify size and MD5
                long localLength = new FileInfo(record.FilePath).Length;
                bool sizeMatches = uploadResult.SizeBytes == localLength;
                bool md5Matches = string.IsNullOrWhiteSpace(uploadResult.Md5Checksum) ||
                                  string.Equals(uploadResult.Md5Checksum, localMd5, StringComparison.OrdinalIgnoreCase);

                if (!sizeMatches || !md5Matches)
                {
                    _logger.LogError("Uploaded backup '{FileName}' verification failed: local size={LocalSize}, Drive size={DriveSize}, local MD5={LocalMd5}, Drive MD5={DriveMd5}. Deleting corrupt file on Drive.",
                        record.FileName, localLength, uploadResult.SizeBytes, localMd5, uploadResult.Md5Checksum);

                    if (!string.IsNullOrWhiteSpace(uploadResult.FileId))
                    {
                        try
                        {
                            await _driveClient.DeleteFileAsync(uploadResult.FileId, false, cancellationToken);
                        }
                        catch { }
                    }

                    record.DriveStatus = DriveUploadStatus.Failed;
                    record.DriveLastError = "Uploaded file MD5/size verification failed.";
                    record.DriveAttempts++;
                    record.DriveNextAttemptAt = CalculateNextAttempt(record.DriveAttempts);
                    await db.SaveChangesAsync(cancellationToken);
                    return true;
                }

                // Upload and verification succeeded
                record.DriveStatus = DriveUploadStatus.Uploaded;
                record.DriveFileId = uploadResult.FileId;
                record.DriveMd5Checksum = uploadResult.Md5Checksum ?? localMd5;
                record.DriveUploadedAt = DateTime.UtcNow;
                record.DriveUploadSessionUri = null;
                record.DriveLastError = null;
                record.DriveNextAttemptAt = null;

                settings.LastSuccessfulDriveUploadAt = DateTime.UtcNow;
                settings.LastDriveUploadError = null;
                settings.GoogleDriveReconnectNeeded = false;
                settings.GoogleDriveIsFull = false;

                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully uploaded and verified backup '{FileName}' on Google Drive.", record.FileName);

                // Run retention policy on Drive
                try
                {
                    await EnforceDriveRetentionAsync(db, settings, subFolderId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Drive retention enforcement encountered an error.");
                }

                return true;
            }
            else
            {
                // Upload failed
                record.DriveAttempts++;
                record.DriveUploadSessionUri = uploadResult.SessionUri;
                record.DriveLastError = uploadResult.ErrorMessage;

                if (uploadResult.IsAuthExpired)
                {
                    record.DriveStatus = DriveUploadStatus.Failed;
                    settings.GoogleDriveReconnectNeeded = true;
                    settings.LastDriveUploadError = "Google Drive authorization expired or revoked. Please reconnect.";
                    await db.SaveChangesAsync(cancellationToken);
                    _logger.LogWarning("Drive upload auth expired for '{FileName}'. Halting queue.", record.FileName);
                    return false; // Stop further uploads
                }

                if (uploadResult.IsQuotaExceeded)
                {
                    record.DriveStatus = DriveUploadStatus.Failed;
                    settings.GoogleDriveIsFull = true;
                    settings.LastDriveUploadError = "Google Drive storage quota is full.";
                    await db.SaveChangesAsync(cancellationToken);
                    _logger.LogWarning("Drive quota exceeded for '{FileName}'. Halting queue.", record.FileName);
                    return false; // Stop further uploads
                }

                if (uploadResult.IsRateLimitedOrServer)
                {
                    record.DriveStatus = DriveUploadStatus.Pending;
                    record.DriveNextAttemptAt = CalculateNextAttempt(record.DriveAttempts);
                    settings.LastDriveUploadError = uploadResult.ErrorMessage;
                    await db.SaveChangesAsync(cancellationToken);
                    _logger.LogWarning("Drive rate limit or server error on '{FileName}'. Backoff scheduled at {Next}.", record.FileName, record.DriveNextAttemptAt);
                    return false; // Back off on server/rate errors
                }

                record.DriveStatus = DriveUploadStatus.Failed;
                record.DriveNextAttemptAt = CalculateNextAttempt(record.DriveAttempts);
                settings.LastDriveUploadError = uploadResult.ErrorMessage;
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }
        });
    }

    public async Task<IReadOnlyList<DriveBackupItemDto>> ListDriveBackupsAsync(CancellationToken cancellationToken = default)
    {
        return await WithDbContextAsync(async db =>
        {
            var setting = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            string? subFolderId = setting?.GoogleDriveSubFolderId;
            if (string.IsNullOrWhiteSpace(subFolderId))
            {
                return (IReadOnlyList<DriveBackupItemDto>)new List<DriveBackupItemDto>();
            }

            var driveFiles = await _driveClient.ListBackupsInFolderAsync(subFolderId, cancellationToken);
            var localRecords = await db.BackupRecords.AsNoTracking().ToListAsync(cancellationToken);

            foreach (var item in driveFiles)
            {
                var match = localRecords.FirstOrDefault(r =>
                    r.DriveFileId == item.Id ||
                    (!string.IsNullOrWhiteSpace(r.DriveMd5Checksum) && string.Equals(r.DriveMd5Checksum, item.Md5Checksum, StringComparison.OrdinalIgnoreCase)) ||
                    string.Equals(r.FileName, item.Name, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    item.ExistsLocally = !match.IsDeletedLocally && File.Exists(match.FilePath);
                    item.LocalBackupRecordId = match.Id;
                }
            }

            return driveFiles;
        });
    }

    public async Task<string> DownloadBackupFromDriveAsync(string driveFileId, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        _appPaths.EnsureDirectoriesExist();

        var driveBackups = await ListDriveBackupsAsync(cancellationToken);
        var targetItem = driveBackups.FirstOrDefault(b => b.Id == driveFileId)
            ?? throw new InvalidOperationException($"Backup file ID '{driveFileId}' not found on Google Drive.");

        string fileName = SanitizeSlug(targetItem.Name);
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            !fileName.EndsWith(".zip.enc", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".zip";
        }

        string destPath = Path.Combine(_appPaths.BackupsDirectory, fileName);
        if (File.Exists(destPath))
        {
            // Ensure unique filename if already exists
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            fileName = $"{nameWithoutExt}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            destPath = Path.Combine(_appPaths.BackupsDirectory, fileName);
        }

        _logger.LogInformation("Downloading backup '{Name}' from Google Drive to '{Dest}'", targetItem.Name, destPath);
        await _driveClient.DownloadFileAsync(driveFileId, destPath, progress, cancellationToken);

        // Verify downloaded file size and checksum
        long downloadedSize = new FileInfo(destPath).Length;
        string downloadedMd5 = await ComputeFileMd5Async(destPath, cancellationToken);

        if (!string.IsNullOrWhiteSpace(targetItem.Md5Checksum) &&
            !string.Equals(targetItem.Md5Checksum, downloadedMd5, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destPath);
            throw new InvalidOperationException("Downloaded backup failed MD5 verification check.");
        }

        // Register as local BackupRecord
        await WithDbContextAsync(async db =>
        {
            string sha256;
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(destPath))
            {
                sha256 = Convert.ToHexString(await sha.ComputeHashAsync(fs, cancellationToken));
            }

            bool isEncrypted = destPath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase);

            var record = new BackupRecord
            {
                FileName = fileName,
                FilePath = destPath,
                SizeBytes = downloadedSize,
                Sha256 = sha256,
                Kind = BackupKind.Manual,
                IsEncrypted = isEncrypted,
                VerifyStatus = BackupVerifyStatus.NotVerified,
                Status = BackupStatus.Created,
                DriveStatus = DriveUploadStatus.Uploaded,
                DriveFileId = driveFileId,
                DriveMd5Checksum = downloadedMd5,
                DriveUploadedAt = targetItem.CreatedTime?.ToUniversalTime() ?? DateTime.UtcNow,
                IsDeletedLocally = false
            };

            db.BackupRecords.Add(record);
            await db.SaveChangesAsync(cancellationToken);
        });

        _logger.LogInformation("Successfully downloaded and registered Google Drive backup at '{Path}'", destPath);
        return destPath;
    }

    public async Task<bool> CheckInternetAccessAsync(CancellationToken cancellationToken = default)
    {
        return await _driveClient.CheckConnectivityAsync(cancellationToken);
    }

    private async Task EnforceDriveRetentionAsync(AppDbContext db, ShopSetting settings, string subFolderId, CancellationToken cancellationToken)
    {
        var driveFiles = await _driveClient.ListBackupsInFolderAsync(subFolderId, cancellationToken);
        if (driveFiles.Count == 0) return;

        int targetKeep = Math.Max(1, settings.KeepDaily + settings.KeepWeekly + settings.KeepMonthly);
        if (driveFiles.Count <= targetKeep) return;

        // Sort descending by date (newest first)
        var sorted = driveFiles
            .OrderByDescending(f => f.CreatedTime ?? DateTime.MinValue)
            .ToList();

        // Never delete the newest uploaded backup!
        var newest = sorted[0];
        var candidates = sorted.Skip(1).ToList();

        var localRecords = await db.BackupRecords.AsNoTracking().ToListAsync(cancellationToken);

        int excessCount = driveFiles.Count - targetKeep;
        int deleted = 0;

        // Evaluate from oldest candidates first
        foreach (var file in candidates.OrderBy(f => f.CreatedTime ?? DateTime.MinValue))
        {
            if (deleted >= excessCount) break;

            if (settings.KeepManualBackupsOnDrive)
            {
                var record = localRecords.FirstOrDefault(r =>
                    r.DriveFileId == file.Id ||
                    string.Equals(r.FileName, file.Name, StringComparison.OrdinalIgnoreCase));

                if (record != null && record.Kind == BackupKind.Manual)
                {
                    // Preserve manual backup
                    continue;
                }
            }

            _logger.LogInformation("Enforcing Drive retention: deleting excess backup '{Name}' ({Id})", file.Name, file.Id);
            try
            {
                await _driveClient.DeleteFileAsync(file.Id, settings.GoogleDriveDeleteToTrash, cancellationToken);
                deleted++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old backup {Id} during Drive retention.", file.Id);
            }
        }
    }

    public static DateTime CalculateNextAttempt(int attemptCount)
    {
        int delayMinutes = attemptCount switch
        {
            1 => 1,
            2 => 5,
            3 => 15,
            4 => 60,
            _ => 360 // 6 hours
        };

        return DateTime.UtcNow.AddMinutes(delayMinutes);
    }

    private static async Task<string> ComputeFileMd5Async(string filePath, CancellationToken cancellationToken)
    {
        using var md5 = MD5.Create();
        await using var stream = File.OpenRead(filePath);
        byte[] hash = await md5.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string SanitizeSlug(string name)
    {
        string sanitized = Regex.Replace(name, @"[^\w\.-]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "ShopPOS" : sanitized;
    }
}

