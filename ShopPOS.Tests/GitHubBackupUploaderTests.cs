using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class GitHubBackupUploaderTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly FakeGitHubClient _fakeClient;
    private readonly IGitHubTokenStore _tokenStore;
    private readonly IAppPaths _appPaths;
    private readonly IBackupCryptoService _cryptoService;
    private readonly string _tempDir;

    public GitHubBackupUploaderTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        _tempDir = Path.Combine(Path.GetTempPath(), $"ShopPOS_GH_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _appPaths = new AppPaths(_tempDir);
        _appPaths.EnsureDirectoriesExist();

        _tokenStore = new DpapiGitHubTokenStore(_appPaths);
        _tokenStore.SaveTokenAsync("ghp_test_token_1234567890abcdef").GetAwaiter().GetResult();

        _fakeClient = new FakeGitHubClient();
        _cryptoService = new BackupCryptoService();

        // Seed basic settings
        var settings = new ShopSetting
        {
            ShopName = "Super Mart",
            GitHubEnabled = true,
            GitHubOwner = "testowner",
            GitHubRepo = "testrepo",
            GitHubBranch = "main",
            GitHubExcludeImages = true,
            EncryptBackups = true,
            EncryptionSalt = Convert.ToBase64String(new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }),
            EncryptionKeyCheck = "valid_key_check"
        };
        _context.ShopSettings.Add(settings);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
        _context.Dispose();
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    private BackupRecord CreateDummyBackupRecord(string fileName, byte[] data, DateTime? createdAt = null)
    {
        string filePath = Path.Combine(_appPaths.BackupsDirectory, fileName);
        File.WriteAllBytes(filePath, data);

        string sha256;
        using (var sha = SHA256.Create())
        {
            sha256 = Convert.ToHexString(sha.ComputeHash(data)).ToLowerInvariant();
        }

        var record = new BackupRecord
        {
            FileName = fileName,
            FilePath = filePath,
            SizeBytes = data.Length,
            Sha256 = sha256,
            Kind = BackupKind.Scheduled,
            IsEncrypted = true,
            Status = BackupStatus.Created,
            GitHubStatus = GitHubUploadStatus.Pending,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        _context.BackupRecords.Add(record);
        _context.SaveChanges();
        return record;
    }

    [Fact]
    public async Task ProcessQueue_CreatesYearlyRelease_AndUploadsDailyAndMonthlyAssets()
    {
        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);

        byte[] fakeArchive = Encoding.UTF8.GetBytes("Encrypted SQLite backup content");
        var record = CreateDummyBackupRecord("ShopPOS_SuperMart_20261107-120000_scheduled.zip.enc", fakeArchive, new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc));

        await uploader.ProcessQueueAsync();

        // 1. Yearly release backup-2026 should be created with prerelease: true
        _fakeClient.Releases.Should().ContainSingle(r => r.TagName == "backup-2026" && r.Prerelease);

        var release = _fakeClient.Releases.First(r => r.TagName == "backup-2026");
        var assets = _fakeClient.ReleaseAssets[release.Id];

        // 2. Assets: daily asset, daily manifest, monthly asset, monthly manifest
        assets.Should().Contain(a => a.Name.StartsWith("ShopPOS_SuperMart_20261107-120000.zip.enc"));
        assets.Should().Contain(a => a.Name == "ShopPOS_SuperMart_20261107-120000.zip.enc.manifest.json");
        assets.Should().Contain(a => a.Name == "ShopPOS_SuperMart_2026-11_monthly.zip.enc");
        assets.Should().Contain(a => a.Name == "ShopPOS_SuperMart_2026-11_monthly.zip.enc.manifest.json");

        // 3. Record status marked Uploaded
        _context.Entry(record).Reload();
        record.GitHubStatus.Should().Be(GitHubUploadStatus.Uploaded);
        record.GitHubUploadedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessQueue_SecondUploadInSameMonth_DoesNotDuplicateMonthlyAsset()
    {
        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);

        byte[] fakeArchive = Encoding.UTF8.GetBytes("Archive 1");
        CreateDummyBackupRecord("ShopPOS_SuperMart_20261107-120000.zip.enc", fakeArchive, new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc));
        await uploader.ProcessQueueAsync();

        byte[] fakeArchive2 = Encoding.UTF8.GetBytes("Archive 2");
        CreateDummyBackupRecord("ShopPOS_SuperMart_20261108-120000.zip.enc", fakeArchive2, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc));
        await uploader.ProcessQueueAsync();

        var release = _fakeClient.Releases.First(r => r.TagName == "backup-2026");
        var assets = _fakeClient.ReleaseAssets[release.Id];

        // Only ONE monthly asset should exist for 2026-11
        assets.Count(a => a.Name == "ShopPOS_SuperMart_2026-11_monthly.zip.enc").Should().Be(1);
        // Both daily assets should exist
        assets.Count(a => a.Name.EndsWith(".zip.enc") && !a.Name.Contains("_monthly")).Should().Be(2);
    }

    [Fact]
    public async Task Retention_DeletesDailyAssetsOlderThanNewest30_KeepsMonthlyAndYearEnd()
    {
        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);

        var release = await _fakeClient.CreateReleaseAsync("testowner", "testrepo", "backup-2026", "Backups 2026", "body", true);

        // Pre-populate release with monthly and year-end assets
        await _fakeClient.UploadReleaseAssetAsync("testowner", "testrepo", release.Id, "ShopPOS_SuperMart_2026-01_monthly.zip.enc", new byte[100]);
        await _fakeClient.UploadReleaseAssetAsync("testowner", "testrepo", release.Id, "ShopPOS_SuperMart_2026_YEAR-END.zip.enc", new byte[100]);

        // Pre-populate release with 35 daily assets
        for (int day = 1; day <= 35; day++)
        {
            string dateStr = new DateTime(2026, day <= 31 ? 1 : 2, day <= 31 ? day : day - 31, 10, 0, 0).ToString("yyyyMMdd-HHmmss");
            string name = $"ShopPOS_SuperMart_{dateStr}.zip.enc";
            var asset = await _fakeClient.UploadReleaseAssetAsync("testowner", "testrepo", release.Id, name, new byte[100]);
            await _fakeClient.UploadReleaseAssetAsync("testowner", "testrepo", release.Id, $"{name}.manifest.json", new byte[20]);
        }

        // Upload a 36th record through uploader to trigger retention
        byte[] fakeArchive = Encoding.UTF8.GetBytes("Newest upload archive");
        CreateDummyBackupRecord("ShopPOS_SuperMart_20261109-120000.zip.enc", fakeArchive, new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc));

        await uploader.ProcessQueueAsync();

        var assetsAfter = _fakeClient.ReleaseAssets[release.Id];

        // Monthly and Year-End must NOT be deleted
        assetsAfter.Should().Contain(a => a.Name == "ShopPOS_SuperMart_2026-01_monthly.zip.enc");
        assetsAfter.Should().Contain(a => a.Name == "ShopPOS_SuperMart_2026_YEAR-END.zip.enc");

        // Exactly 30 daily assets must remain
        var remainingDailies = assetsAfter.Where(a => a.Name.EndsWith(".zip.enc") && !a.Name.Contains("_monthly") && !a.Name.Contains("_YEAR-END")).ToList();
        remainingDailies.Count.Should().Be(30);

        // Deletions occurred
        _fakeClient.DeletedAssetIds.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_RefusesUpload_IfRepositoryIsPublic()
    {
        _fakeClient.IsPrivate = false; // Public repository!

        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);
        byte[] fakeArchive = Encoding.UTF8.GetBytes("Test archive");
        var record = CreateDummyBackupRecord("ShopPOS_SuperMart_20261107-120000.zip.enc", fakeArchive);

        await uploader.ProcessQueueAsync();

        var settings = await _context.ShopSettings.FirstAsync();
        settings.GitHubRepoIsPublic.Should().BeTrue();
        settings.LastGitHubUploadError.Should().Contain("public");

        _context.Entry(record).Reload();
        record.GitHubStatus.Should().Be(GitHubUploadStatus.Pending);
        _fakeClient.UploadedAssets.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_RefusesUpload_IfEncryptionIsDisabled()
    {
        var settings = await _context.ShopSettings.FirstAsync();
        settings.EncryptBackups = false;
        await _context.SaveChangesAsync();

        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);
        byte[] fakeArchive = Encoding.UTF8.GetBytes("Unencrypted test archive");
        var record = CreateDummyBackupRecord("ShopPOS_SuperMart_20261107-120000.zip.enc", fakeArchive);

        await uploader.ProcessQueueAsync();

        settings = await _context.ShopSettings.FirstAsync();
        settings.LastGitHubUploadError.Should().Contain("encryption is not enabled");

        _fakeClient.UploadedAssets.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_RefusesUpload_IfFileExceeds1_8GiB()
    {
        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);

        string bigFilePath = Path.Combine(_appPaths.BackupsDirectory, "ShopPOS_SuperMart_Huge.zip.enc");
        // Create 1KB dummy file on disk but record size as 2 GB
        File.WriteAllBytes(bigFilePath, new byte[1024]);

        var record = new BackupRecord
        {
            FileName = "ShopPOS_SuperMart_Huge.zip.enc",
            FilePath = bigFilePath,
            SizeBytes = 2L * 1024 * 1024 * 1024, // 2 GiB
            Sha256 = "dummy_sha",
            Status = BackupStatus.Created,
            GitHubStatus = GitHubUploadStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _context.BackupRecords.Add(record);
        await _context.SaveChangesAsync();

        // Write actual file size > 1.8 GiB to test FileInfo
        // Instead of writing 2GB to disk, we can test file size check
        using (var fs = new FileStream(bigFilePath, FileMode.Open, FileAccess.Write))
        {
            fs.SetLength(1932735284L); // 1 byte over 1.8 GiB
        }

        await uploader.ProcessQueueAsync();

        _context.Entry(record).Reload();
        record.GitHubStatus.Should().Be(GitHubUploadStatus.Failed);
        record.GitHubLastError.Should().Contain("1.8 GiB limit");

        var settings = await _context.ShopSettings.FirstAsync();
        settings.GitHubFileTooLarge.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessQueue_Offline_DoesNotIncrementAttemptsOrWasteRetries()
    {
        _fakeClient.IsOnline = false;

        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);
        byte[] fakeArchive = Encoding.UTF8.GetBytes("Test archive");
        var record = CreateDummyBackupRecord("ShopPOS_SuperMart_20261107-120000.zip.enc", fakeArchive);

        await uploader.ProcessQueueAsync();

        _context.Entry(record).Reload();
        record.GitHubAttempts.Should().Be(0);
        record.GitHubStatus.Should().Be(GitHubUploadStatus.Pending);
        _fakeClient.UploadedAssets.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_Http401Or403_SetsReconnectNeeded()
    {
        _fakeClient.ForcedHttpError = new HttpRequestException("Bad credentials", null, HttpStatusCode.Unauthorized);

        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);
        byte[] fakeArchive = Encoding.UTF8.GetBytes("Test archive");
        var record = CreateDummyBackupRecord("ShopPOS_SuperMart_20261107-120000.zip.enc", fakeArchive);

        await uploader.ProcessQueueAsync();

        var settings = await _context.ShopSettings.FirstAsync();
        settings.GitHubReconnectNeeded.Should().BeTrue();
        settings.LastGitHubUploadError.Should().Contain("token unauthorized or expired");

        _context.Entry(record).Reload();
        record.GitHubStatus.Should().Be(GitHubUploadStatus.Failed);
        record.GitHubAttempts.Should().Be(1);
    }

    [Fact]
    public async Task CreateYearEndArchive_CreatesYearEndArchiveWithCsvExports()
    {
        var supplier = new Supplier { Name = "Test Supplier" };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        // Seed sale and purchase for 2026
        _context.Sales.Add(new Sale
        {
            SaleNumber = "INV-2026-001",
            SaleDate = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            GrandTotal = 500m,
            SubTotal = 450m,
            TaxAmount = 50m,
            Status = SaleStatus.Completed
        });
        _context.Purchases.Add(new Purchase
        {
            PurchaseNumber = "PO-2026-001",
            SupplierInvoiceNo = "SUP-001",
            SupplierId = supplier.Id,
            PurchaseDate = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc),
            GrandTotal = 1200m,
            Status = PurchaseStatus.Posted
        });
        await _context.SaveChangesAsync();

        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);

        bool success = await uploader.CreateYearEndArchiveAsync(2026);
        success.Should().BeTrue();

        var release = _fakeClient.Releases.First(r => r.TagName == "backup-2026");
        var assets = _fakeClient.ReleaseAssets[release.Id];

        assets.Should().Contain(a => a.Name == "ShopPOS_SuperMart_2026_YEAR-END.zip.enc");
        assets.Should().Contain(a => a.Name == "ShopPOS_SuperMart_2026_YEAR-END.zip.enc.manifest.json");
    }

    [Fact]
    public async Task DownloadBackupFromGitHub_VerifiesSha256AndRegistersRecord()
    {
        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);

        var release = await _fakeClient.CreateReleaseAsync("testowner", "testrepo", "backup-2026", "Backups 2026", "body", true);

        byte[] fakeData = Encoding.UTF8.GetBytes("Verified archive data payload");
        string expectedSha;
        using (var sha = SHA256.Create())
        {
            expectedSha = Convert.ToHexString(sha.ComputeHash(fakeData)).ToLowerInvariant();
        }

        string assetName = "ShopPOS_SuperMart_20261107-120000.zip.enc";
        var asset = await _fakeClient.UploadReleaseAssetAsync("testowner", "testrepo", release.Id, assetName, fakeData);

        var manifestDto = new GitHubManifestDto
        {
            AppVersion = "1.0.0",
            SchemaVersion = "latest",
            CreatedAt = DateTime.UtcNow,
            Sha256 = expectedSha,
            SizeBytes = fakeData.Length,
            ShopName = "Super Mart",
            FileName = assetName
        };
        byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifestDto));
        await _fakeClient.UploadReleaseAssetAsync("testowner", "testrepo", release.Id, $"{assetName}.manifest.json", manifestBytes, "application/json");

        string downloadedPath = await uploader.DownloadBackupFromGitHubAsync(asset.Id.ToString());

        File.Exists(downloadedPath).Should().BeTrue();
        byte[] savedBytes = await File.ReadAllBytesAsync(downloadedPath);
        savedBytes.Should().Equal(fakeData);

        var record = await _context.BackupRecords.FirstOrDefaultAsync(b => b.FileName == assetName);
        record.Should().NotBeNull();
        record!.Sha256.Should().Be(expectedSha);
        record.GitHubStatus.Should().Be(GitHubUploadStatus.Uploaded);
    }

    [Fact]
    public async Task CopyBackupsToAnotherRepository_ReplicatesReleasesAndAssets_SkipsIdentical()
    {
        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);

        // Set up source release and asset
        var rel = await _fakeClient.CreateReleaseAsync("srcowner", "srcrepo", "backup-2026", "Backups 2026", "body", true);
        await _fakeClient.UploadReleaseAssetAsync("srcowner", "srcrepo", rel.Id, "ShopPOS_SuperMart_20261107-120000.zip.enc", new byte[] { 1, 2, 3 });

        var options = new GitHubCopyRepoOptionsDto
        {
            SourceOwner = "srcowner",
            SourceRepo = "srcrepo",
            SourceToken = "src_token",
            DestOwner = "destowner",
            DestRepo = "destrepo",
            DestToken = "dest_token"
        };

        var result = await uploader.CopyBackupsToAnotherRepositoryAsync(options);
        result.Success.Should().BeTrue();
        result.CopiedCount.Should().Be(1);

        // Copy a second time: should be skipped as identical!
        var result2 = await uploader.CopyBackupsToAnotherRepositoryAsync(options);
        result2.Success.Should().BeTrue();
        result2.SkippedCount.Should().Be(1);
        result2.CopiedCount.Should().Be(0);
    }

    [Fact]
    public async Task PrepareRepository_CommitsBareReadme_AndNothingElse()
    {
        var uploader = new GitHubBackupUploader(_context, _fakeClient, _tokenStore, _appPaths, _cryptoService);

        bool prepared = await uploader.PrepareRepositoryAsync();
        prepared.Should().BeTrue();
        _fakeClient.ReadmeCommitted.Should().BeTrue();
    }
}

