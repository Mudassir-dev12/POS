using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AwesomeAssertions;
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

public class BackupUploaderTests : IDisposable
{
    private readonly string _testDir;
    private readonly IAppPaths _appPaths;
    private readonly AppDbContext _context;
    private readonly FakeDriveClient _fakeDriveClient;
    private readonly IDriveTokenStore _tokenStore;
    private readonly IGoogleDriveConfigService _configService;

    public BackupUploaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"ShopPOS_UploaderTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _appPaths = new AppPaths(_testDir);
        _appPaths.EnsureDirectoriesExist();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_testDir, "test.db")}")
            .Options;
        _context = new AppDbContext(options);
        _context.Database.Migrate();

        _fakeDriveClient = new FakeDriveClient();
        _tokenStore = new DpapiDriveTokenStore(_appPaths);

        // Preconfigure client config
        File.WriteAllText(_appPaths.GoogleClientConfigPath, "{\"client_id\":\"test-id\",\"client_secret\":\"test-secret\"}");
        _configService = new GoogleDriveConfigService(_appPaths);
    }

    public void Dispose()
    {
        _context.Dispose();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task ConnectAsync_SetsAccountEmailAndCreatesFolders()
    {
        var uploader = new BackupUploader(_context, _fakeDriveClient, _tokenStore, _configService, _appPaths);

        bool result = await uploader.ConnectAsync();
        result.Should().BeTrue();

        var settings = await _context.ShopSettings.FirstAsync();
        settings.GoogleDriveEnabled.Should().BeTrue();
        settings.GoogleDriveAccountEmail.Should().Be("testshop@gmail.com");
        settings.GoogleDriveFolderId.Should().NotBeNullOrWhiteSpace();
        settings.GoogleDriveSubFolderId.Should().NotBeNullOrWhiteSpace();
        settings.GoogleDriveReconnectNeeded.Should().BeFalse();
        settings.GoogleDriveIsFull.Should().BeFalse();

        _fakeDriveClient.CreatedFolderNames.Should().Contain("ShopPOS Backups");
    }

    [Fact]
    public async Task DisconnectAsync_ClearsSettingsAndRevokesToken()
    {
        var uploader = new BackupUploader(_context, _fakeDriveClient, _tokenStore, _configService, _appPaths);
        await uploader.ConnectAsync();

        await uploader.DisconnectAsync();

        var settings = await _context.ShopSettings.FirstAsync();
        settings.GoogleDriveEnabled.Should().BeFalse();
        settings.GoogleDriveAccountEmail.Should().BeNull();
        settings.GoogleDriveFolderId.Should().BeNull();
        settings.GoogleDriveSubFolderId.Should().BeNull();
        _fakeDriveClient.IsTokenRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessQueueAsync_UploadsInFIFOOrder_OldestFirst()
    {
        var uploader = new BackupUploader(_context, _fakeDriveClient, _tokenStore, _configService, _appPaths);
        await uploader.ConnectAsync();

        // Create 2 local backup files
        string file1 = Path.Combine(_appPaths.BackupsDirectory, "backup_old.zip");
        string file2 = Path.Combine(_appPaths.BackupsDirectory, "backup_new.zip");
        await File.WriteAllBytesAsync(file1, new byte[] { 1, 2, 3 });
        await File.WriteAllBytesAsync(file2, new byte[] { 4, 5, 6 });

        var rOld = new BackupRecord
        {
            FileName = "backup_old.zip",
            FilePath = file1,
            SizeBytes = 3,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            DriveStatus = DriveUploadStatus.Pending,
            DriveMd5Checksum = Convert.ToHexString(MD5.HashData(new byte[] { 1, 2, 3 }))
        };
        var rNew = new BackupRecord
        {
            FileName = "backup_new.zip",
            FilePath = file2,
            SizeBytes = 3,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            DriveStatus = DriveUploadStatus.Pending,
            DriveMd5Checksum = Convert.ToHexString(MD5.HashData(new byte[] { 4, 5, 6 }))
        };

        _context.BackupRecords.AddRange(rOld, rNew);
        await _context.SaveChangesAsync();

        await uploader.ProcessQueueAsync(force: true);

        _fakeDriveClient.UploadedFilePaths.Count.Should().Be(2);
        _fakeDriveClient.UploadedFilePaths[0].Should().Be(file1);
        _fakeDriveClient.UploadedFilePaths[1].Should().Be(file2);

        var refreshedOld = await _context.BackupRecords.FindAsync(rOld.Id);
        refreshedOld!.DriveStatus.Should().Be(DriveUploadStatus.Uploaded);
        refreshedOld.DriveUploadedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessQueueAsync_WhenOffline_SetsWaitingForInternet()
    {
        var uploader = new BackupUploader(_context, _fakeDriveClient, _tokenStore, _configService, _appPaths);
        await uploader.ConnectAsync();

        _fakeDriveClient.IsOnline = false;

        string file = Path.Combine(_appPaths.BackupsDirectory, "backup_test.zip");
        await File.WriteAllBytesAsync(file, new byte[] { 1 });
        var record = new BackupRecord
        {
            FileName = "backup_test.zip",
            FilePath = file,
            SizeBytes = 1,
            DriveStatus = DriveUploadStatus.Pending
        };
        _context.BackupRecords.Add(record);
        await _context.SaveChangesAsync();

        await uploader.ProcessQueueAsync(force: true);

        var summary = await uploader.GetStatusSummaryAsync();
        summary.IsWaitingForInternet.Should().BeTrue();
        _fakeDriveClient.UploadedFilePaths.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessQueueAsync_Handles401AuthExpired_SetsReconnectNeededAndHaltsQueue()
    {
        var uploader = new BackupUploader(_context, _fakeDriveClient, _tokenStore, _configService, _appPaths);
        await uploader.ConnectAsync();

        _fakeDriveClient.SimulateAuthExpired = true;

        string file = Path.Combine(_appPaths.BackupsDirectory, "backup_auth.zip");
        await File.WriteAllBytesAsync(file, new byte[] { 1 });
        _context.BackupRecords.Add(new BackupRecord
        {
            FileName = "backup_auth.zip",
            FilePath = file,
            SizeBytes = 1,
            DriveStatus = DriveUploadStatus.Pending
        });
        await _context.SaveChangesAsync();

        await uploader.ProcessQueueAsync(force: true);

        var settings = await _context.ShopSettings.FirstAsync();
        settings.GoogleDriveReconnectNeeded.Should().BeTrue();

        var summary = await uploader.GetStatusSummaryAsync();
        summary.ReconnectNeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessQueueAsync_Handles403QuotaExceeded_SetsDriveFullAndHaltsQueue()
    {
        var uploader = new BackupUploader(_context, _fakeDriveClient, _tokenStore, _configService, _appPaths);
        await uploader.ConnectAsync();

        _fakeDriveClient.SimulateQuotaExceeded = true;

        string file = Path.Combine(_appPaths.BackupsDirectory, "backup_quota.zip");
        await File.WriteAllBytesAsync(file, new byte[] { 1 });
        _context.BackupRecords.Add(new BackupRecord
        {
            FileName = "backup_quota.zip",
            FilePath = file,
            SizeBytes = 1,
            DriveStatus = DriveUploadStatus.Pending
        });
        await _context.SaveChangesAsync();

        await uploader.ProcessQueueAsync(force: true);

        var settings = await _context.ShopSettings.FirstAsync();
        settings.GoogleDriveIsFull.Should().BeTrue();

        var summary = await uploader.GetStatusSummaryAsync();
        summary.IsDriveFull.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessQueueAsync_OnMd5Mismatch_DeletesBadFileFromDriveAndRetries()
    {
        var uploader = new BackupUploader(_context, _fakeDriveClient, _tokenStore, _configService, _appPaths);
        await uploader.ConnectAsync();

        _fakeDriveClient.SimulateMd5Mismatch = true;

        string file = Path.Combine(_appPaths.BackupsDirectory, "backup_corrupt.zip");
        await File.WriteAllBytesAsync(file, new byte[] { 1, 2, 3 });
        var record = new BackupRecord
        {
            FileName = "backup_corrupt.zip",
            FilePath = file,
            SizeBytes = 3,
            DriveStatus = DriveUploadStatus.Pending,
            DriveMd5Checksum = Convert.ToHexString(MD5.HashData(new byte[] { 1, 2, 3 }))
        };
        _context.BackupRecords.Add(record);
        await _context.SaveChangesAsync();

        await uploader.ProcessQueueAsync(force: true);

        // Mismatched file was deleted from Drive
        _fakeDriveClient.DeletedFileIds.Count.Should().Be(1);

        var refreshed = await _context.BackupRecords.FindAsync(record.Id);
        refreshed!.DriveStatus.Should().Be(DriveUploadStatus.Failed);
        refreshed.DriveAttempts.Should().Be(1);
        refreshed.DriveNextAttemptAt.Should().NotBeNull();
    }

    [Fact]
    public void CalculateNextAttempt_ExponentialBackoffIncreasesCorrectly()
    {
        var a1 = BackupUploader.CalculateNextAttempt(1);
        var a2 = BackupUploader.CalculateNextAttempt(2);
        var a3 = BackupUploader.CalculateNextAttempt(3);
        var a4 = BackupUploader.CalculateNextAttempt(4);
        var a5 = BackupUploader.CalculateNextAttempt(5);

        (a1 - DateTime.UtcNow).TotalMinutes.Should().BeInRange(0.8, 1.2);
        (a2 - DateTime.UtcNow).TotalMinutes.Should().BeInRange(4.8, 5.2);
        (a3 - DateTime.UtcNow).TotalMinutes.Should().BeInRange(14.8, 15.2);
        (a4 - DateTime.UtcNow).TotalMinutes.Should().BeInRange(59.5, 60.5);
        (a5 - DateTime.UtcNow).TotalMinutes.Should().BeInRange(358.0, 362.0);
    }

    [Fact]
    public async Task DownloadBackupFromDrive_VerifiesMd5AndRegistersLocalRecord()
    {
        var uploader = new BackupUploader(_context, _fakeDriveClient, _tokenStore, _configService, _appPaths);
        await uploader.ConnectAsync();

        byte[] payload = new byte[] { 10, 20, 30, 40 };
        string payloadMd5 = Convert.ToHexString(MD5.HashData(payload));

        var subFolderId = (await _context.ShopSettings.FirstAsync()).GoogleDriveSubFolderId;
        var fakeFile = new FakeDriveClient.FakeDriveFile
        {
            Name = "ShopPOS_CloudArchive.zip",
            ParentFolderId = subFolderId,
            SizeBytes = payload.Length,
            Md5Checksum = payloadMd5,
            ContentBytes = payload,
            CreatedTime = DateTime.UtcNow.AddDays(-1)
        };
        _fakeDriveClient.Files.Add(fakeFile);

        string downloadedPath = await uploader.DownloadBackupFromDriveAsync(fakeFile.Id);

        File.Exists(downloadedPath).Should().BeTrue();
        (new FileInfo(downloadedPath).Length).Should().Be(4);

        var localRecord = await _context.BackupRecords.FirstOrDefaultAsync(r => r.FileName == Path.GetFileName(downloadedPath));
        localRecord.Should().NotBeNull();
        localRecord!.DriveFileId.Should().Be(fakeFile.Id);
        localRecord.DriveStatus.Should().Be(DriveUploadStatus.Uploaded);
    }
}

