using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
/// Orchestrates background automatic uploads of encrypted backup archives to a private GitHub repository
/// using GitHub Release Assets (uploads.github.com), yearly releases (backup-yyyy), monthly permanent assets,
/// year-end archives, 30-day daily retention, 1.8 GiB asset limit, and repo-to-repo migration.
/// </summary>
public class GitHubBackupUploader : IGitHubBackupUploader
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly AppDbContext? _dbContext;
    private readonly IGitHubClient _gitHubClient;
    private readonly IGitHubTokenStore _tokenStore;
    private readonly IBackupCryptoService? _cryptoService;
    private readonly IAppPaths _appPaths;
    private readonly ILogger<GitHubBackupUploader> _logger;

    private readonly SemaphoreSlim _queueLock = new(1, 1);
    public bool IsQueueBusy => _queueLock.CurrentCount == 0;

    private const long SizeWarningBytes = 500L * 1024 * 1024; // 500 MB
    private const long SizeLimitBytes = 1932735283L; // 1.8 GiB (1.8 * 1024 * 1024 * 1024)

    // 1-hour in-memory safety check cache
    private string? _cachedOwner;
    private string? _cachedRepo;
    private bool _cachedIsPrivate;
    private int _cachedAccessibleRepoCount;
    private DateTime _cacheExpiresAt = DateTime.MinValue;

    [ActivatorUtilitiesConstructor]
    public GitHubBackupUploader(
        IServiceScopeFactory scopeFactory,
        IGitHubClient gitHubClient,
        IGitHubTokenStore tokenStore,
        IAppPaths appPaths,
        IBackupCryptoService? cryptoService = null,
        ILogger<GitHubBackupUploader>? logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        _cryptoService = cryptoService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubBackupUploader>.Instance;
    }

    internal GitHubBackupUploader(
        AppDbContext dbContext,
        IGitHubClient gitHubClient,
        IGitHubTokenStore tokenStore,
        IAppPaths appPaths,
        IBackupCryptoService? cryptoService = null,
        ILogger<GitHubBackupUploader>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        _cryptoService = cryptoService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubBackupUploader>.Instance;
    }

    public static string GetSlug(string? shopName)
    {
        if (string.IsNullOrWhiteSpace(shopName)) return "ShopPOS";
        var clean = new string(shopName.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "ShopPOS" : clean;
    }

    public async Task<GitHubTestResultDto> TestConnectionAsync(string owner, string repo, string branch, string? token = null, CancellationToken cancellationToken = default)
    {
        var result = new GitHubTestResultDto();

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
        {
            result.Success = false;
            result.ErrorMessage = "Owner and repository name are required.";
            return result;
        }

        string? activeToken = token;
        if (string.IsNullOrWhiteSpace(activeToken))
        {
            activeToken = await _tokenStore.GetTokenAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(activeToken))
        {
            result.Success = false;
            result.ErrorMessage = "Personal access token is required.";
            return result;
        }

        try
        {
            var repoInfo = await _gitHubClient.GetRepositoryAsync(owner, repo, activeToken, cancellationToken);
            if (!repoInfo.Exists)
            {
                result.Success = false;
                result.ErrorMessage = repoInfo.ErrorMessage ?? "Repository not found or access denied.";
                return result;
            }

            result.IsPrivate = repoInfo.IsPrivate;
            result.RepoFullName = $"{owner}/{repo}";
            result.DefaultBranch = repoInfo.DefaultBranch;

            if (!repoInfo.IsPrivate)
            {
                result.Success = false;
                result.ErrorMessage = "Target repository is PUBLIC. For security reasons, backups will only upload to a PRIVATE repository.";
                return result;
            }

            var accessible = await _gitHubClient.ListAccessibleRepositoriesAsync(activeToken, cancellationToken);
            result.AccessibleRepoCount = accessible.Count;
            if (accessible.Count > 1)
            {
                result.WarningMessage = $"Personal access token can access {accessible.Count} repositories. A fine-grained token restricted strictly to '{owner}/{repo}' is recommended.";
            }

            _cachedOwner = owner;
            _cachedRepo = repo;
            _cachedIsPrivate = repoInfo.IsPrivate;
            _cachedAccessibleRepoCount = accessible.Count;
            _cacheExpiresAt = DateTime.UtcNow.AddHours(1);

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test GitHub connection");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async Task<bool> ConfigureAsync(GitHubConfigDto config, CancellationToken cancellationToken = default)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        if (!string.IsNullOrWhiteSpace(config.Token))
        {
            await _tokenStore.SaveTokenAsync(config.Token, cancellationToken);
        }

        using var scope = _scopeFactory?.CreateScope();
        var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;

        var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null)
        {
            settings = new ShopSetting { ShopName = "My Shop" };
            db.ShopSettings.Add(settings);
        }

        settings.GitHubEnabled = config.Enabled;
        settings.GitHubOwner = config.Owner?.Trim();
        settings.GitHubRepo = config.Repo?.Trim();
        settings.GitHubBranch = string.IsNullOrWhiteSpace(config.Branch) ? "main" : config.Branch.Trim();
        settings.GitHubExcludeImages = config.ExcludeImages;
        settings.GitHubReconnectNeeded = false;
        settings.GitHubRepoIsPublic = false;
        settings.GitHubFileTooLarge = false;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<GitHubStatusSummaryDto> GetGitHubStatusSummaryAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory?.CreateScope();
        var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;

        var settings = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new ShopSetting();
        bool hasToken = await _tokenStore.HasTokenAsync(cancellationToken);

        int pendingCount = 0;
        if (settings.GitHubEnabled)
        {
            pendingCount = await db.BackupRecords.CountAsync(b =>
                !b.IsDeleted &&
                b.Status == BackupStatus.Created &&
                (b.GitHubStatus == GitHubUploadStatus.Pending || b.GitHubStatus == GitHubUploadStatus.NotConfigured || b.GitHubStatus == GitHubUploadStatus.Failed),
                cancellationToken);
        }

        return new GitHubStatusSummaryDto
        {
            IsEnabled = settings.GitHubEnabled,
            IsConfigured = !string.IsNullOrWhiteSpace(settings.GitHubOwner) && !string.IsNullOrWhiteSpace(settings.GitHubRepo) && hasToken,
            HasToken = hasToken,
            Owner = settings.GitHubOwner,
            Repo = settings.GitHubRepo,
            Branch = settings.GitHubBranch,
            LastUploadAt = settings.LastSuccessfulGitHubUploadAt,
            LastError = settings.LastGitHubUploadError,
            ReconnectNeeded = settings.GitHubReconnectNeeded,
            RepoIsPublic = settings.GitHubRepoIsPublic,
            FileTooLarge = settings.GitHubFileTooLarge,
            PendingCount = pendingCount,
            LastYearEndArchiveAt = settings.LastGitHubSquashAt
        };
    }

    public async Task<bool> PrepareRepositoryAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory?.CreateScope();
        var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;

        var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null || string.IsNullOrWhiteSpace(settings.GitHubOwner) || string.IsNullOrWhiteSpace(settings.GitHubRepo))
        {
            return false;
        }

        string? token = await _tokenStore.GetTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token)) return false;

        return await _gitHubClient.EnsureReadmeCommittedAsync(settings.GitHubOwner, settings.GitHubRepo, token, cancellationToken);
    }

    public async Task ProcessQueueAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!await _queueLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("GitHub upload queue is already running; skipping trigger.");
            return;
        }

        try
        {
            using var scope = _scopeFactory?.CreateScope();
            var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;

            var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings == null || !settings.GitHubEnabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.GitHubOwner) || string.IsNullOrWhiteSpace(settings.GitHubRepo))
            {
                _logger.LogWarning("GitHub backup is enabled but owner or repo is not configured.");
                return;
            }

            bool isOnline = await _gitHubClient.CheckConnectivityAsync(cancellationToken);
            if (!isOnline)
            {
                _logger.LogWarning("No internet access to GitHub; deferring upload queue without penalizing retry count.");
                return;
            }

            string? token = await _tokenStore.GetTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                settings.GitHubReconnectNeeded = true;
                settings.LastGitHubUploadError = "GitHub personal access token missing. Please reconnect in Settings.";
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            bool isPrivate = await EnsureRepoIsPrivateCachedAsync(settings.GitHubOwner, settings.GitHubRepo, token, cancellationToken);
            if (!isPrivate)
            {
                settings.GitHubRepoIsPublic = true;
                settings.LastGitHubUploadError = "GitHub repository is public. For security reasons, backups will only upload to a private repository.";
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            settings.GitHubRepoIsPublic = false;

            if (!settings.EncryptBackups || string.IsNullOrWhiteSpace(settings.EncryptionSalt))
            {
                settings.LastGitHubUploadError = "Backup encryption is not enabled. Backups must be encrypted before uploading to GitHub.";
                _logger.LogWarning("Refusing GitHub upload: encryption is not enabled.");
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var now = DateTime.UtcNow;
            var candidates = await db.BackupRecords
                .Where(b => !b.IsDeleted && b.Status == BackupStatus.Created)
                .Where(b => b.GitHubStatus == GitHubUploadStatus.Pending ||
                            b.GitHubStatus == GitHubUploadStatus.NotConfigured ||
                            (b.GitHubStatus == GitHubUploadStatus.Failed && (force || (b.GitHubNextAttemptAt != null && b.GitHubNextAttemptAt <= now))))
                .OrderBy(b => b.CreatedAt)
                .ToListAsync(cancellationToken);

            bool anyUploaded = false;
            foreach (var record in candidates)
            {
                bool success = await UploadSingleBackupCoreAsync(record.Id, cancellationToken);
                if (success) anyUploaded = true;
            }

            if (anyUploaded)
            {
                // Check if year-end archive for previous year should be generated automatically
                await CheckYearEndArchiveTriggerAsync(settings, token, cancellationToken);
            }
        }
        catch (HttpRequestException hex)
        {
            _logger.LogError(hex, "HTTP error in GitHub backup upload queue");
            try
            {
                using var scope = _scopeFactory?.CreateScope();
                var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;
                var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
                if (settings != null)
                {
                    if (hex.StatusCode == HttpStatusCode.Unauthorized || hex.StatusCode == HttpStatusCode.Forbidden)
                    {
                        settings.GitHubReconnectNeeded = true;
                        string msg = "GitHub token unauthorized or expired. Please re-enter personal access token in Settings.";
                        settings.LastGitHubUploadError = msg;

                        var pendingRecords = await db.BackupRecords
                            .Where(b => !b.IsDeleted && b.Status == BackupStatus.Created &&
                                       (b.GitHubStatus == GitHubUploadStatus.Pending || b.GitHubStatus == GitHubUploadStatus.NotConfigured))
                            .ToListAsync(cancellationToken);

                        foreach (var rec in pendingRecords)
                        {
                            rec.GitHubAttempts++;
                            rec.GitHubStatus = GitHubUploadStatus.Failed;
                            rec.GitHubLastError = msg;
                            rec.GitHubNextAttemptAt = CalculateNextAttempt(rec.GitHubAttempts);
                        }
                    }
                    else
                    {
                        settings.LastGitHubUploadError = hex.Message;
                    }
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to update settings after HTTP error in queue");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GitHub backup upload queue");
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task<bool> UploadSingleBackupAsync(int backupRecordId, CancellationToken cancellationToken = default)
    {
        await _queueLock.WaitAsync(cancellationToken);
        try
        {
            return await UploadSingleBackupCoreAsync(backupRecordId, cancellationToken);
        }
        finally
        {
            _queueLock.Release();
        }
    }

    private async Task<bool> UploadSingleBackupCoreAsync(int backupRecordId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory?.CreateScope();
        var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;

        var record = await db.BackupRecords.FirstOrDefaultAsync(b => b.Id == backupRecordId, cancellationToken);
        var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);

        if (record == null || settings == null || !settings.GitHubEnabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.GitHubOwner) || string.IsNullOrWhiteSpace(settings.GitHubRepo))
        {
            return false;
        }

        string? token = await _tokenStore.GetTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            settings.GitHubReconnectNeeded = true;
            settings.LastGitHubUploadError = "GitHub token missing.";
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        string archivePath = record.FilePath;
        if (!File.Exists(archivePath))
        {
            archivePath = Path.Combine(_appPaths.BackupsDirectory, record.FileName);
        }

        if (!File.Exists(archivePath))
        {
            record.GitHubStatus = GitHubUploadStatus.Failed;
            record.GitHubLastError = $"Backup file not found at '{record.FilePath}'";
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        var fileInfo = new FileInfo(archivePath);
        long sizeBytes = fileInfo.Length;

        // Size rules: warn > 500 MB, refuse > 1.8 GiB
        if (sizeBytes > SizeLimitBytes)
        {
            string err = $"Backup file exceeds 1.8 GiB limit (current size: {(double)sizeBytes / (1024 * 1024):F1} MB). Exclude product images from backup or archive older data.";
            _logger.LogError("{Error}", err);
            record.GitHubStatus = GitHubUploadStatus.Failed;
            record.GitHubLastError = err;
            settings.GitHubFileTooLarge = true;
            settings.LastGitHubUploadError = err;
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        if (sizeBytes > SizeWarningBytes)
        {
            _logger.LogWarning("Backup archive exceeds 500 MB (size: {SizeMb:F1} MB). Consider excluding product images.", (double)sizeBytes / (1024 * 1024));
        }

        record.GitHubStatus = GitHubUploadStatus.Uploading;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(archivePath, cancellationToken);

            string localSha256;
            using (var sha = SHA256.Create())
            {
                localSha256 = Convert.ToHexString(sha.ComputeHash(fileBytes)).ToLowerInvariant();
            }

            string owner = settings.GitHubOwner;
            string repo = settings.GitHubRepo;
            int year = record.CreatedAt.Year;
            string releaseTag = $"backup-{year}";

            // 1. Get or create yearly release (backup-<yyyy>)
            var release = await _gitHubClient.GetReleaseByTagAsync(owner, repo, releaseTag, token, cancellationToken);
            if (release == null)
            {
                release = await _gitHubClient.CreateReleaseAsync(
                    owner,
                    repo,
                    releaseTag,
                    $"Backups {year}",
                    $"Automated annual backup release for {year}.",
                    prerelease: true,
                    token,
                    cancellationToken);
            }

            // 2. Build Daily Asset Name & Manifest
            string slug = GetSlug(settings.ShopName);
            string timestampStr = record.CreatedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string dailyAssetName = $"ShopPOS_{slug}_{timestampStr}.zip.enc";
            string dailyManifestName = $"{dailyAssetName}.manifest.json";

            var manifestDto = new GitHubManifestDto
            {
                AppVersion = record.AppVersion,
                SchemaVersion = record.SchemaVersion,
                CreatedAt = record.CreatedAt,
                Sha256 = localSha256,
                SizeBytes = sizeBytes,
                ShopName = settings.ShopName,
                FileName = dailyAssetName,
                ExcludedImages = settings.GitHubExcludeImages,
                IsYearEnd = false,
                Year = year
            };

            byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifestDto, new JsonSerializerOptions { WriteIndented = true }));

            // Upload daily asset
            var uploadedDailyAsset = await _gitHubClient.UploadReleaseAssetAsync(
                owner, repo, release.Id, dailyAssetName, fileBytes, "application/octet-stream", token, null, cancellationToken);

            // Upload daily manifest asset
            await _gitHubClient.UploadReleaseAssetAsync(
                owner, repo, release.Id, dailyManifestName, manifestBytes, "application/json", token, null, cancellationToken);

            // 3. Permanent Monthly Snapshot Check
            // The first successful upload of each calendar month is also uploaded under: ShopPOS_<slug>_<yyyy-MM>_monthly.zip.enc
            string monthStr = record.CreatedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            string monthlyAssetName = $"ShopPOS_{slug}_{monthStr}_monthly.zip.enc";
            string monthlyManifestName = $"{monthlyAssetName}.manifest.json";

            var existingAssets = await _gitHubClient.ListReleaseAssetsAsync(owner, repo, release.Id, token, cancellationToken);
            bool monthlyAlreadyExists = existingAssets.Any(a => string.Equals(a.Name, monthlyAssetName, StringComparison.OrdinalIgnoreCase));

            if (!monthlyAlreadyExists)
            {
                var monthlyManifestDto = new GitHubManifestDto
                {
                    AppVersion = record.AppVersion,
                    SchemaVersion = record.SchemaVersion,
                    CreatedAt = record.CreatedAt,
                    Sha256 = localSha256,
                    SizeBytes = sizeBytes,
                    ShopName = settings.ShopName,
                    FileName = monthlyAssetName,
                    ExcludedImages = settings.GitHubExcludeImages,
                    IsYearEnd = false,
                    Year = year
                };
                byte[] monthlyManifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(monthlyManifestDto, new JsonSerializerOptions { WriteIndented = true }));

                await _gitHubClient.UploadReleaseAssetAsync(
                    owner, repo, release.Id, monthlyAssetName, fileBytes, "application/octet-stream", token, null, cancellationToken);

                await _gitHubClient.UploadReleaseAssetAsync(
                    owner, repo, release.Id, monthlyManifestName, monthlyManifestBytes, "application/json", token, null, cancellationToken);

                _logger.LogInformation("Uploaded permanent monthly backup asset '{MonthlyName}' to release {Tag}", monthlyAssetName, releaseTag);
            }

            // 4. Post-upload verification: Download uploaded daily asset, verify size and SHA-256
            byte[] remoteBytes = await _gitHubClient.DownloadReleaseAssetAsync(owner, repo, uploadedDailyAsset.Id, token, null, cancellationToken);
            if (remoteBytes.Length != fileBytes.Length)
            {
                throw new InvalidOperationException($"Post-upload verification failed: size mismatch (Local: {fileBytes.Length}, Remote: {remoteBytes.Length})");
            }

            string remoteSha256;
            using (var sha = SHA256.Create())
            {
                remoteSha256 = Convert.ToHexString(sha.ComputeHash(remoteBytes)).ToLowerInvariant();
            }

            if (!string.Equals(localSha256, remoteSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Post-upload verification failed: SHA-256 checksum mismatch (Local: {localSha256}, Remote: {remoteSha256})");
            }

            // 5. Apply Retention on GitHub:
            // Keep newest 30 daily assets of current year. Keep all monthly and year-end assets.
            // Delete older daily assets and their .manifest.json through API. Never delete the newest asset.
            await EnforceReleaseAssetRetentionAsync(owner, repo, release.Id, token, cancellationToken);

            // Verified successfully!
            record.GitHubStatus = GitHubUploadStatus.Uploaded;
            record.GitHubFileSha = uploadedDailyAsset.Id.ToString(CultureInfo.InvariantCulture);
            record.GitHubUploadedAt = DateTime.UtcNow;
            record.GitHubLastError = null;
            record.GitHubAttempts = 0;
            record.GitHubNextAttemptAt = null;

            settings.LastSuccessfulGitHubUploadAt = DateTime.UtcNow;
            settings.LastGitHubUploadError = null;
            settings.GitHubReconnectNeeded = false;
            settings.GitHubFileTooLarge = false;

            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully uploaded and verified backup record #{Id} to GitHub Release {Tag} ('{Asset}')", record.Id, releaseTag, dailyAssetName);
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during GitHub release asset upload for record #{Id}", record.Id);
            HandleHttpError(record, settings, ex);
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload backup record #{Id} to GitHub", record.Id);
            record.GitHubStatus = GitHubUploadStatus.Failed;
            record.GitHubLastError = ex.Message;
            record.GitHubAttempts++;
            record.GitHubNextAttemptAt = CalculateNextAttempt(record.GitHubAttempts);

            settings.LastGitHubUploadError = ex.Message;
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }
    }

    private async Task EnforceReleaseAssetRetentionAsync(string owner, string repo, long releaseId, string token, CancellationToken ct)
    {
        try
        {
            var assets = await _gitHubClient.ListReleaseAssetsAsync(owner, repo, releaseId, token, ct);

            // Filter assets matching our app pattern: ShopPOS_<slug>_<date/kind>
            var appAssets = assets.Where(a => a.Name.StartsWith("ShopPOS_", StringComparison.OrdinalIgnoreCase)).ToList();

            // Identify daily zip assets (not _monthly, not _YEAR-END, not .manifest.json)
            var dailyZipAssets = appAssets
                .Where(a => a.Name.EndsWith(".zip.enc", StringComparison.OrdinalIgnoreCase) &&
                            !a.Name.Contains("_monthly", StringComparison.OrdinalIgnoreCase) &&
                            !a.Name.Contains("_YEAR-END", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            if (dailyZipAssets.Count <= 30)
            {
                return; // 30 or fewer daily assets, nothing to prune
            }

            // Keep the newest 30 daily assets. Never delete the newest asset!
            var toDelete = dailyZipAssets.Skip(30).ToList();

            foreach (var oldAsset in toDelete)
            {
                _logger.LogInformation("Pruning expired daily release asset '{Name}' (Id: {Id})", oldAsset.Name, oldAsset.Id);
                await _gitHubClient.DeleteReleaseAssetAsync(owner, repo, oldAsset.Id, token, ct);

                // Also find and delete its matching manifest
                string manifestName = $"{oldAsset.Name}.manifest.json";
                var manifestAsset = appAssets.FirstOrDefault(a => string.Equals(a.Name, manifestName, StringComparison.OrdinalIgnoreCase));
                if (manifestAsset != null)
                {
                    await _gitHubClient.DeleteReleaseAssetAsync(owner, repo, manifestAsset.Id, token, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Retention pruning encountered an error for release {ReleaseId}", releaseId);
        }
    }

    private async Task CheckYearEndArchiveTriggerAsync(ShopSetting settings, string token, CancellationToken ct)
    {
        try
        {
            int currentYear = DateTime.UtcNow.Year;
            int previousYear = currentYear - 1;

            // Check if year-end archive already exists on release backup-<previousYear>
            string tag = $"backup-{previousYear}";
            var release = await _gitHubClient.GetReleaseByTagAsync(settings.GitHubOwner!, settings.GitHubRepo!, tag, token, ct);
            if (release != null)
            {
                var assets = await _gitHubClient.ListReleaseAssetsAsync(settings.GitHubOwner!, settings.GitHubRepo!, release.Id, token, ct);
                bool hasYearEnd = assets.Any(a => a.Name.Contains("_YEAR-END.zip.enc", StringComparison.OrdinalIgnoreCase));
                if (!hasYearEnd)
                {
                    _logger.LogInformation("First upload of the new year detected; generating Year-End archive for {Year}...", previousYear);
                    await CreateYearEndArchiveAsync(previousYear, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check or trigger automated Year-End archive");
        }
    }

    public async Task<bool> CreateYearEndArchiveAsync(int? targetYear = null, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory?.CreateScope();
        var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;

        var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null || string.IsNullOrWhiteSpace(settings.GitHubOwner) || string.IsNullOrWhiteSpace(settings.GitHubRepo))
        {
            return false;
        }

        string? token = await _tokenStore.GetTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token)) return false;

        int year = targetYear ?? (DateTime.UtcNow.Month == 1 ? DateTime.UtcNow.Year - 1 : DateTime.UtcNow.Year);
        string owner = settings.GitHubOwner;
        string repo = settings.GitHubRepo;
        string releaseTag = $"backup-{year}";

        var release = await _gitHubClient.GetReleaseByTagAsync(owner, repo, releaseTag, token, cancellationToken);
        if (release == null)
        {
            release = await _gitHubClient.CreateReleaseAsync(
                owner, repo, releaseTag, $"Backups {year}", $"Annual backup release for {year}.", prerelease: true, token, cancellationToken);
        }

        string slug = GetSlug(settings.ShopName);
        string yearEndAssetName = $"ShopPOS_{slug}_{year}_YEAR-END.zip.enc";
        string yearEndManifestName = $"{yearEndAssetName}.manifest.json";

        _appPaths.EnsureDirectoriesExist();
        string tempDir = Path.Combine(_appPaths.TempDirectory, $"yearend_{year}_{Guid.NewGuid():N}");
        string contentDir = Path.Combine(tempDir, "content");
        Directory.CreateDirectory(contentDir);

        try
        {
            // 1. Snapshot database
            string tempDbPath = Path.Combine(contentDir, "shop.db");
            if (File.Exists(_appPaths.DatabasePath))
            {
                File.Copy(_appPaths.DatabasePath, tempDbPath, overwrite: true);
            }

            // 2. Generate CSV exports for that year
            string csvDir = Path.Combine(contentDir, "CSV");
            Directory.CreateDirectory(csvDir);

            var startOfYear = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfYear = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

            // Sales CSV
            var sales = await db.Sales.AsNoTracking()
                .Where(s => s.SaleDate >= startOfYear && s.SaleDate <= endOfYear)
                .OrderBy(s => s.SaleDate)
                .ToListAsync(cancellationToken);

            var sbSales = new StringBuilder();
            sbSales.AppendLine("SaleNumber,SaleDate,CustomerName,CustomerPhone,GrandTotal,SubTotal,TaxAmount,Status");
            foreach (var s in sales)
            {
                sbSales.AppendLine($"\"{s.SaleNumber}\",\"{s.SaleDate:s}\",\"{s.CustomerName}\",\"{s.CustomerPhone}\",{s.GrandTotal},{s.SubTotal},{s.TaxAmount},\"{s.Status}\"");
            }
            await File.WriteAllTextAsync(Path.Combine(csvDir, $"Sales_{year}.csv"), sbSales.ToString(), Encoding.UTF8, cancellationToken);

            // Purchases CSV
            var purchases = await db.Purchases.AsNoTracking()
                .Where(p => p.PurchaseDate >= startOfYear && p.PurchaseDate <= endOfYear)
                .OrderBy(p => p.PurchaseDate)
                .ToListAsync(cancellationToken);

            var sbPurchases = new StringBuilder();
            sbPurchases.AppendLine("PurchaseNumber,SupplierInvoiceNo,PurchaseDate,GrandTotal,Status");
            foreach (var p in purchases)
            {
                sbPurchases.AppendLine($"\"{p.PurchaseNumber}\",\"{p.SupplierInvoiceNo}\",\"{p.PurchaseDate:s}\",{p.GrandTotal},\"{p.Status}\"");
            }
            await File.WriteAllTextAsync(Path.Combine(csvDir, $"Purchases_{year}.csv"), sbPurchases.ToString(), Encoding.UTF8, cancellationToken);

            // Expenses CSV
            var expenses = await db.Expenses.AsNoTracking()
                .Where(e => e.ExpenseDate >= startOfYear && e.ExpenseDate <= endOfYear)
                .OrderBy(e => e.ExpenseDate)
                .ToListAsync(cancellationToken);

            var sbExpenses = new StringBuilder();
            sbExpenses.AppendLine("ExpenseNumber,ExpenseDate,Amount,Notes");
            foreach (var e in expenses)
            {
                sbExpenses.AppendLine($"\"{e.ExpenseNumber}\",\"{e.ExpenseDate:s}\",{e.Amount},\"{e.Notes}\"");
            }
            await File.WriteAllTextAsync(Path.Combine(csvDir, $"Expenses_{year}.csv"), sbExpenses.ToString(), Encoding.UTF8, cancellationToken);

            // 3. Zip files
            string unencryptedZip = Path.Combine(tempDir, "archive.zip");
            ZipFile.CreateFromDirectory(contentDir, unencryptedZip);

            // 4. Encrypt zip
            string encryptedZip = Path.Combine(tempDir, "archive.zip.enc");
            byte[] encryptedBytes;

            if (_cryptoService != null && settings.EncryptBackups)
            {
                string encKey = settings.EncryptionSalt ?? "ShopPOS_YearEnd_Key";
                _cryptoService.EncryptFile(unencryptedZip, encryptedZip, encKey);
                encryptedBytes = await File.ReadAllBytesAsync(encryptedZip, cancellationToken);
            }
            else
            {
                encryptedBytes = await File.ReadAllBytesAsync(unencryptedZip, cancellationToken);
            }

            // Compute SHA-256
            string sha256;
            using (var sha = SHA256.Create())
            {
                sha256 = Convert.ToHexString(sha.ComputeHash(encryptedBytes)).ToLowerInvariant();
            }

            // 5. Upload Year-End Asset
            await _gitHubClient.UploadReleaseAssetAsync(
                owner, repo, release.Id, yearEndAssetName, encryptedBytes, "application/octet-stream", token, null, cancellationToken);

            // 6. Upload Manifest
            var manifest = new GitHubManifestDto
            {
                AppVersion = "1.0.0",
                SchemaVersion = "latest",
                CreatedAt = DateTime.UtcNow,
                Sha256 = sha256,
                SizeBytes = encryptedBytes.Length,
                ShopName = settings.ShopName,
                FileName = yearEndAssetName,
                ExcludedImages = settings.GitHubExcludeImages,
                IsYearEnd = true,
                Year = year
            };
            byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            await _gitHubClient.UploadReleaseAssetAsync(
                owner, repo, release.Id, yearEndManifestName, manifestBytes, "application/json", token, null, cancellationToken);

            settings.LastGitHubSquashAt = DateTime.UtcNow; // Used to track last year-end archive created
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully created and uploaded permanent Year-End archive '{Asset}'", yearEndAssetName);
            return true;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
            catch { }
        }
    }

    public async Task<IReadOnlyList<GitHubBackupItemDto>> ListGitHubBackupsAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<GitHubBackupItemDto>();

        using var scope = _scopeFactory?.CreateScope();
        var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;

        var settings = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (settings == null || string.IsNullOrWhiteSpace(settings.GitHubOwner) || string.IsNullOrWhiteSpace(settings.GitHubRepo))
        {
            return items;
        }

        string? token = await _tokenStore.GetTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token)) return items;

        string owner = settings.GitHubOwner;
        string repo = settings.GitHubRepo;

        try
        {
            var releases = await _gitHubClient.ListReleasesAsync(owner, repo, token, cancellationToken);
            foreach (var release in releases.OrderByDescending(r => r.TagName))
            {
                var assets = await _gitHubClient.ListReleaseAssetsAsync(owner, repo, release.Id, token, cancellationToken);

                // Filter zip.enc assets
                var zipAssets = assets.Where(a => a.Name.EndsWith(".zip.enc", StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var asset in zipAssets)
                {
                    bool isYearEnd = asset.Name.Contains("_YEAR-END", StringComparison.OrdinalIgnoreCase);
                    bool isMonthly = asset.Name.Contains("_monthly", StringComparison.OrdinalIgnoreCase);
                    bool isDaily = !isYearEnd && !isMonthly;

                    items.Add(new GitHubBackupItemDto
                    {
                        AssetId = asset.Id,
                        ReleaseId = release.Id,
                        ReleaseTagName = release.TagName,
                        FileName = asset.Name,
                        CreatedAt = asset.CreatedAt,
                        SizeBytes = asset.Size,
                        IsDaily = isDaily,
                        IsMonthly = isMonthly,
                        IsYearEnd = isYearEnd,
                        DownloadUrl = asset.BrowserDownloadUrl
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list releases and assets from GitHub");
        }

        return items.OrderByDescending(i => i.CreatedAt).ToList();
    }

    public async Task<string> DownloadBackupFromGitHubAsync(string assetIdOrName, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory?.CreateScope();
        var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;

        var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null || string.IsNullOrWhiteSpace(settings.GitHubOwner) || string.IsNullOrWhiteSpace(settings.GitHubRepo))
        {
            throw new InvalidOperationException("GitHub is not configured.");
        }

        string? token = await _tokenStore.GetTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("GitHub personal access token is missing.");
        }

        string owner = settings.GitHubOwner;
        string repo = settings.GitHubRepo;

        var allItems = await ListGitHubBackupsAsync(cancellationToken);
        GitHubBackupItemDto? targetItem = null;

        if (long.TryParse(assetIdOrName, out long assetId))
        {
            targetItem = allItems.FirstOrDefault(i => i.AssetId == assetId);
        }
        if (targetItem == null)
        {
            targetItem = allItems.FirstOrDefault(i => string.Equals(i.FileName, assetIdOrName, StringComparison.OrdinalIgnoreCase));
        }

        if (targetItem == null)
        {
            throw new FileNotFoundException($"GitHub release asset '{assetIdOrName}' was not found.");
        }

        _appPaths.EnsureDirectoriesExist();

        // 1. Fetch manifest if available
        GitHubManifestDto? manifest = null;
        try
        {
            var assets = await _gitHubClient.ListReleaseAssetsAsync(owner, repo, targetItem.ReleaseId, token, cancellationToken);
            string manifestName = $"{targetItem.FileName}.manifest.json";
            var manifestAsset = assets.FirstOrDefault(a => string.Equals(a.Name, manifestName, StringComparison.OrdinalIgnoreCase));
            if (manifestAsset != null)
            {
                string manifestJson = await _gitHubClient.GetAssetContentAsStringAsync(owner, repo, manifestAsset.Id, token, cancellationToken);
                manifest = JsonSerializer.Deserialize<GitHubManifestDto>(manifestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch manifest for asset '{FileName}'", targetItem.FileName);
        }

        // 2. Download asset bytes
        byte[] downloadedBytes = await _gitHubClient.DownloadReleaseAssetAsync(owner, repo, targetItem.AssetId, token, progress, cancellationToken);

        // 3. Verify SHA-256
        string computedSha256;
        using (var sha = SHA256.Create())
        {
            computedSha256 = Convert.ToHexString(sha.ComputeHash(downloadedBytes)).ToLowerInvariant();
        }

        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            if (!string.Equals(computedSha256, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Downloaded backup integrity failure: SHA-256 checksum mismatch (Expected: {manifest.Sha256}, Actual: {computedSha256})");
            }
        }

        string localTarget = Path.Combine(_appPaths.BackupsDirectory, targetItem.FileName);
        await File.WriteAllBytesAsync(localTarget, downloadedBytes, cancellationToken);

        // 4. Register local BackupRecord
        var record = new BackupRecord
        {
            FileName = targetItem.FileName,
            FilePath = localTarget,
            SizeBytes = downloadedBytes.Length,
            Sha256 = computedSha256,
            Kind = BackupKind.Manual,
            IsEncrypted = targetItem.FileName.EndsWith(".enc", StringComparison.OrdinalIgnoreCase),
            SchemaVersion = manifest?.SchemaVersion ?? string.Empty,
            AppVersion = manifest?.AppVersion ?? "1.0.0",
            VerifyStatus = BackupVerifyStatus.Ok,
            VerifiedAt = DateTime.UtcNow,
            Status = BackupStatus.Created,
            GitHubStatus = GitHubUploadStatus.Uploaded,
            GitHubUploadedAt = DateTime.UtcNow
        };

        db.BackupRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Downloaded backup '{FileName}' from GitHub to '{Target}'", targetItem.FileName, localTarget);
        return localTarget;
    }

    public async Task<GitHubCopyResultDto> CopyBackupsToAnotherRepositoryAsync(GitHubCopyRepoOptionsDto options, IProgress<GitHubCopyProgressDto>? progress = null, CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        var result = new GitHubCopyResultDto();

        if (string.IsNullOrWhiteSpace(options.DestToken) || string.IsNullOrWhiteSpace(options.DestOwner) || string.IsNullOrWhiteSpace(options.DestRepo))
        {
            result.Success = false;
            result.Messages.Add("Destination owner, repository and token are required.");
            return result;
        }

        try
        {
            // Case 1: Copying from another GitHub repository
            if (!string.IsNullOrWhiteSpace(options.SourceOwner) && !string.IsNullOrWhiteSpace(options.SourceRepo))
            {
                string? srcToken = options.SourceToken;
                if (string.IsNullOrWhiteSpace(srcToken))
                {
                    srcToken = await _tokenStore.GetTokenAsync(cancellationToken);
                }

                var srcReleases = await _gitHubClient.ListReleasesAsync(options.SourceOwner, options.SourceRepo, srcToken, cancellationToken);
                var destReleases = await _gitHubClient.ListReleasesAsync(options.DestOwner, options.DestRepo, options.DestToken, cancellationToken);

                var allAssetsToCopy = new List<(GitHubReleaseDto Release, GitHubReleaseAssetDto Asset)>();
                foreach (var rel in srcReleases)
                {
                    var assets = await _gitHubClient.ListReleaseAssetsAsync(options.SourceOwner, options.SourceRepo, rel.Id, srcToken, cancellationToken);
                    foreach (var a in assets.Where(x => x.Name.StartsWith("ShopPOS_", StringComparison.OrdinalIgnoreCase)))
                    {
                        allAssetsToCopy.Add((rel, a));
                    }
                }

                result.TotalFound = allAssetsToCopy.Count;
                int current = 0;

                foreach (var item in allAssetsToCopy)
                {
                    current++;
                    progress?.Report(new GitHubCopyProgressDto
                    {
                        CurrentItem = item.Asset.Name,
                        CopiedCount = result.CopiedCount,
                        SkippedCount = result.SkippedCount,
                        TotalCount = result.TotalFound,
                        Percent = (current * 100) / result.TotalFound,
                        Message = $"Processing {item.Asset.Name}..."
                    });

                    // Ensure target release exists
                    var destRel = destReleases.FirstOrDefault(r => string.Equals(r.TagName, item.Release.TagName, StringComparison.OrdinalIgnoreCase));
                    if (destRel == null)
                    {
                        destRel = await _gitHubClient.CreateReleaseAsync(
                            options.DestOwner, options.DestRepo, item.Release.TagName, item.Release.Name, "Migrated backup release", prerelease: true, options.DestToken, cancellationToken);
                        destReleases = await _gitHubClient.ListReleasesAsync(options.DestOwner, options.DestRepo, options.DestToken, cancellationToken);
                    }

                    var destAssets = await _gitHubClient.ListReleaseAssetsAsync(options.DestOwner, options.DestRepo, destRel.Id, options.DestToken, cancellationToken);
                    bool alreadyExists = destAssets.Any(da => string.Equals(da.Name, item.Asset.Name, StringComparison.OrdinalIgnoreCase) && da.Size == item.Asset.Size);
                    if (alreadyExists)
                    {
                        result.SkippedCount++;
                        result.Messages.Add($"Skipped '{item.Asset.Name}' (already exists with identical size).");
                        continue;
                    }

                    // Download from source and upload to dest
                    byte[] assetBytes = await _gitHubClient.DownloadReleaseAssetAsync(options.SourceOwner, options.SourceRepo, item.Asset.Id, srcToken, null, cancellationToken);
                    await _gitHubClient.UploadReleaseAssetAsync(
                        options.DestOwner, options.DestRepo, destRel.Id, item.Asset.Name, assetBytes, "application/octet-stream", options.DestToken, null, cancellationToken);

                    result.CopiedCount++;
                    result.Messages.Add($"Copied '{item.Asset.Name}' to '{options.DestOwner}/{options.DestRepo}'.");
                }

                result.Success = true;
                return result;
            }

            // Case 2: Copying from local folder of downloaded files
            if (!string.IsNullOrWhiteSpace(options.SourceLocalFolder) && Directory.Exists(options.SourceLocalFolder))
            {
                var files = Directory.GetFiles(options.SourceLocalFolder, "ShopPOS_*.*", SearchOption.AllDirectories);
                result.TotalFound = files.Length;
                int current = 0;

                var destReleases = await _gitHubClient.ListReleasesAsync(options.DestOwner, options.DestRepo, options.DestToken, cancellationToken);

                foreach (var filePath in files)
                {
                    current++;
                    string fileName = Path.GetFileName(filePath);

                    // Determine year from filename or current year
                    int year = DateTime.UtcNow.Year;
                    var parts = fileName.Split('_');
                    foreach (var p in parts)
                    {
                        if (p.Length >= 4 && int.TryParse(p.Substring(0, 4), out int parsedYear) && parsedYear >= 2020 && parsedYear <= 2099)
                        {
                            year = parsedYear;
                            break;
                        }
                    }

                    string tagName = $"backup-{year}";
                    var destRel = destReleases.FirstOrDefault(r => string.Equals(r.TagName, tagName, StringComparison.OrdinalIgnoreCase));
                    if (destRel == null)
                    {
                        destRel = await _gitHubClient.CreateReleaseAsync(
                            options.DestOwner, options.DestRepo, tagName, $"Backups {year}", "Restored backup release", prerelease: true, options.DestToken, cancellationToken);
                        destReleases = await _gitHubClient.ListReleasesAsync(options.DestOwner, options.DestRepo, options.DestToken, cancellationToken);
                    }

                    var destAssets = await _gitHubClient.ListReleaseAssetsAsync(options.DestOwner, options.DestRepo, destRel.Id, options.DestToken, cancellationToken);
                    var fi = new FileInfo(filePath);
                    bool alreadyExists = destAssets.Any(da => string.Equals(da.Name, fileName, StringComparison.OrdinalIgnoreCase) && da.Size == fi.Length);
                    if (alreadyExists)
                    {
                        result.SkippedCount++;
                        result.Messages.Add($"Skipped '{fileName}' (already exists with identical size).");
                        continue;
                    }

                    byte[] fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                    string contentType = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "application/json" : "application/octet-stream";
                    await _gitHubClient.UploadReleaseAssetAsync(
                        options.DestOwner, options.DestRepo, destRel.Id, fileName, fileBytes, contentType, options.DestToken, null, cancellationToken);

                    result.CopiedCount++;
                    result.Messages.Add($"Uploaded local '{fileName}' to '{options.DestOwner}/{options.DestRepo}'.");
                }

                result.Success = true;
                return result;
            }

            result.Success = false;
            result.Messages.Add("Neither valid source repository nor local folder was specified.");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy backups to destination repository");
            result.Success = false;
            result.Messages.Add($"Error: {ex.Message}");
            return result;
        }
    }

    private void HandleHttpError(BackupRecord record, ShopSetting settings, HttpRequestException ex)
    {
        record.GitHubAttempts++;
        record.GitHubNextAttemptAt = CalculateNextAttempt(record.GitHubAttempts);
        record.GitHubStatus = GitHubUploadStatus.Failed;

        if (ex.StatusCode == HttpStatusCode.Unauthorized || ex.StatusCode == HttpStatusCode.Forbidden)
        {
            settings.GitHubReconnectNeeded = true;
            string msg = "GitHub token unauthorized or expired. Please re-enter personal access token in Settings.";
            record.GitHubLastError = msg;
            settings.LastGitHubUploadError = msg;
        }
        else if (ex.StatusCode == HttpStatusCode.NotFound)
        {
            string msg = $"Repository '{settings.GitHubOwner}/{settings.GitHubRepo}' not found or token has no access.";
            record.GitHubLastError = msg;
            settings.LastGitHubUploadError = msg;
        }
        else
        {
            record.GitHubLastError = ex.Message;
            settings.LastGitHubUploadError = ex.Message;
        }
    }

    public static DateTime CalculateNextAttempt(int attemptCount)
    {
        var now = DateTime.UtcNow;
        return attemptCount switch
        {
            1 => now.AddMinutes(1),
            2 => now.AddMinutes(5),
            3 => now.AddMinutes(15),
            4 => now.AddHours(1),
            _ => now.AddHours(6)
        };
    }

    private async Task<bool> EnsureRepoIsPrivateCachedAsync(string owner, string repo, string token, CancellationToken ct)
    {
        if (_cachedOwner == owner && _cachedRepo == repo && DateTime.UtcNow < _cacheExpiresAt)
        {
            return _cachedIsPrivate;
        }

        var repoInfo = await _gitHubClient.GetRepositoryAsync(owner, repo, token, ct);
        if (!repoInfo.Exists)
        {
            return false;
        }

        _cachedOwner = owner;
        _cachedRepo = repo;
        _cachedIsPrivate = repoInfo.IsPrivate;
        _cacheExpiresAt = DateTime.UtcNow.AddHours(1);
        return repoInfo.IsPrivate;
    }

    #region IBackupUploader implementation (bridge for uploader-agnostic calls)

    public async Task<bool> ConnectAsync(Func<string, Task>? onBrowserOpen = null, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory?.CreateScope();
        var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;
        var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null) return false;

        var test = await TestConnectionAsync(settings.GitHubOwner ?? "", settings.GitHubRepo ?? "", settings.GitHubBranch ?? "main", null, cancellationToken);
        return test.Success;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _tokenStore.DeleteTokenAsync(cancellationToken);

        using var scope = _scopeFactory?.CreateScope();
        var db = scope != null ? scope.ServiceProvider.GetRequiredService<AppDbContext>() : _dbContext!;
        var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings != null)
        {
            settings.GitHubEnabled = false;
            settings.GitHubReconnectNeeded = false;
            settings.LastGitHubUploadError = null;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<DriveStatusSummaryDto> GetStatusSummaryAsync(CancellationToken cancellationToken = default)
    {
        var gh = await GetGitHubStatusSummaryAsync(cancellationToken);
        return new DriveStatusSummaryDto
        {
            IsConnected = gh.IsConfigured && !gh.ReconnectNeeded,
            IsConfiguredInBuild = gh.IsConfigured,
            AccountEmail = $"{gh.Owner}/{gh.Repo} (Releases)",
            LastUploadAt = gh.LastUploadAt,
            LastError = gh.LastError,
            ReconnectNeeded = gh.ReconnectNeeded,
            IsDriveFull = gh.FileTooLarge,
            PendingUploadCount = gh.PendingCount
        };
    }

    public async Task<IReadOnlyList<DriveBackupItemDto>> ListDriveBackupsAsync(CancellationToken cancellationToken = default)
    {
        var list = await ListGitHubBackupsAsync(cancellationToken);
        return list.Select(g => new DriveBackupItemDto
        {
            Id = g.AssetId.ToString(CultureInfo.InvariantCulture),
            Name = g.FileName,
            SizeBytes = g.SizeBytes,
            CreatedTime = g.CreatedAt,
            Md5Checksum = g.Sha256 ?? string.Empty
        }).ToList();
    }

    public Task<string> DownloadBackupFromDriveAsync(string driveFileId, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        return DownloadBackupFromGitHubAsync(driveFileId, progress, cancellationToken);
    }

    public Task<bool> CheckInternetAccessAsync(CancellationToken cancellationToken = default)
    {
        return _gitHubClient.CheckConnectivityAsync(cancellationToken);
    }

    #endregion
}
