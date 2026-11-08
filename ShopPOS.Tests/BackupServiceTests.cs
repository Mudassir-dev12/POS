using System.IO.Compression;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShopPOS.Core;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly IAppPaths _appPaths;
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _context;
    private readonly IBackupService _backupService;
    private readonly IBackupCryptoService _crypto;

    public BackupServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"BackupSvcTest_{Guid.NewGuid():N}");
        _appPaths = new AppPaths(_testRoot);
        _appPaths.EnsureDirectoriesExist();

        var services = new ServiceCollection();
        services.AddSingleton<IAppPaths>(_appPaths);
        services.AddCoreServices();
        services.AddDataServices(new DatabaseOptions { DbPath = _appPaths.DatabasePath });

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<AppDbContext>();
        _context.Database.Migrate();

        _backupService = _serviceProvider.GetRequiredService<IBackupService>();
        _crypto = _serviceProvider.GetRequiredService<IBackupCryptoService>();

        var category = new Category { Name = "General" };
        var unit = new Unit { Name = "Piece", ShortName = "pc" };
        _context.Categories.Add(category);
        _context.Units.Add(unit);

        // Seed basic settings
        var settings = _context.ShopSettings.FirstOrDefault();
        if (settings == null)
        {
            settings = new ShopSetting { ShopName = "Test Mart" };
            _context.ShopSettings.Add(settings);
        }
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateBackup_DuringActiveWalWrites_ProducesConsistentValidDatabase()
    {
        // Insert sample products
        for (int i = 1; i <= 20; i++)
        {
            _context.Products.Add(new Product
            {
                Name = $"Product {i}",
                Sku = $"SKU-{i:D4}",
                CostPrice = 1000,
                SalePrice = 1500,
                CategoryId = 1,
                UnitId = 1
            });
        }
        await _context.SaveChangesAsync();

        // Start continuous background writer in WAL mode
        using var cts = new CancellationTokenSource();
        var writeTask = Task.Run(async () =>
        {
            using var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}");
            await conn.OpenAsync(cts.Token);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            await cmd.ExecuteNonQueryAsync(cts.Token);

            int counter = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    cmd.CommandText = $"INSERT INTO \"Products\" (Name, Sku, CostPrice, SalePrice, StockQuantity, CreatedAt, IsDeleted) VALUES ('Async Prod {counter}', 'ASYNC-{counter}', 500, 800, 10, datetime('now'), 0);";
                    await cmd.ExecuteNonQueryAsync(cts.Token);
                    counter++;
                    await Task.Delay(5, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, cts.Token);

        // Take online snapshot backup while writer is writing
        var result = await _backupService.CreateBackupAsync(BackupKind.Manual);

        cts.Cancel();
        try { await writeTask; } catch { }

        result.Success.Should().BeTrue();
        result.FileName.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.FilePath).Should().BeTrue();

        // Verify the created backup
        var verify = await _backupService.VerifyBackupFileAsync(result.FilePath!);
        verify.IsValid.Should().BeTrue();
        verify.Status.Should().Be(BackupVerifyStatus.Ok);
    }

    [Fact]
    public async Task CreateBackup_ArchiveContainsShopDbManifestAndImages()
    {
        // Create a dummy image in images directory
        string testImg = Path.Combine(_appPaths.ImagesDirectory, "prod1.jpg");
        File.WriteAllText(testImg, "image-content");

        var result = await _backupService.CreateBackupAsync(BackupKind.Scheduled);
        result.Success.Should().BeTrue();

        using (var zip = ZipFile.OpenRead(result.FilePath!))
        {
            zip.GetEntry("shop.db").Should().NotBeNull();
            var manifestEntry = zip.GetEntry("manifest.json");
            manifestEntry.Should().NotBeNull();

            using var stream = manifestEntry!.Open();
            var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream);
            manifest.Should().NotBeNull();
            manifest!.AppVersion.Should().NotBeNullOrWhiteSpace();
            manifest.SchemaVersion.Should().NotBeNullOrWhiteSpace();
            manifest.DatabaseSha256.Should().NotBeNullOrWhiteSpace();

            zip.GetEntry("Images/prod1.jpg").Should().NotBeNull();
        }
    }

    [Fact]
    public async Task VerifyBackup_DetectsCorruptedZipOrDatabase()
    {
        var result = await _backupService.CreateBackupAsync(BackupKind.Manual);
        result.Success.Should().BeTrue();

        string corruptedPath = Path.Combine(_appPaths.TempDirectory, "corrupted.zip");
        File.Copy(result.FilePath!, corruptedPath, true);

        // Tamper with the zip content
        byte[] bytes = File.ReadAllBytes(corruptedPath);
        for (int i = bytes.Length / 2; i < (bytes.Length / 2) + 50; i++)
        {
            bytes[i] ^= 0xAA;
        }
        File.WriteAllBytes(corruptedPath, bytes);

        var verify = await _backupService.VerifyBackupFileAsync(corruptedPath);
        verify.IsValid.Should().BeFalse();
        verify.Status.Should().Be(BackupVerifyStatus.Failed);
    }

    [Fact]
    public async Task CreateBackup_WithEncryption_ProducesEncryptedArchive()
    {
        string password = "StrongBackupPassword789!";
        var result = await _backupService.CreateBackupAsync(BackupKind.Manual, password);

        result.Success.Should().BeTrue();
        result.FilePath!.EndsWith(".zip.enc").Should().BeTrue();
        _crypto.IsEncryptedBackup(result.FilePath).Should().BeTrue();

        // Verify with password succeeds
        var verify = await _backupService.VerifyBackupFileAsync(result.FilePath, password);
        verify.IsValid.Should().BeTrue();

        // Verify with wrong password fails
        var verifyWrong = await _backupService.VerifyBackupFileAsync(result.FilePath, "WrongPassword");
        verifyWrong.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateBackup_SecondFolderMissing_OnlyWarnsAndDoesNotFail()
    {
        var settings = await _context.ShopSettings.FirstAsync();
        settings.SecondFolderPath = @"Z:\NonExistentDrive\Backups";
        await _context.SaveChangesAsync();

        var result = await _backupService.CreateBackupAsync(BackupKind.Manual);

        result.Success.Should().BeTrue();
        result.WarningMessage.Should().NotBeNullOrWhiteSpace();
        result.WarningMessage.Should().Contain("Secondary backup folder");
    }

    [Fact]
    public async Task GrandfatherFatherSonRetention_400DaysHistory_RetainsCorrectBucketsAndNeverDeletesProtected()
    {
        var settings = await _context.ShopSettings.FirstAsync();
        settings.KeepDaily = 7;
        settings.KeepWeekly = 4;
        settings.KeepMonthly = 6;
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;

        // Generate simulated backup records over 400 days
        for (int day = 400; day >= 1; day--)
        {
            var date = now.AddDays(-day);
            string name = $"Sim_Backup_{day}.zip";
            string path = Path.Combine(_appPaths.BackupsDirectory, name);
            File.WriteAllText(path, $"dummy backup day {day}");

            _context.BackupRecords.Add(new BackupRecord
            {
                FileName = name,
                FilePath = path,
                CreatedAt = date,
                SizeBytes = 1024,
                Sha256 = "dummy",
                Kind = BackupKind.Scheduled,
                Status = BackupStatus.Created,
                DriveStatus = DriveUploadStatus.NotConfigured,
                IsDeletedLocally = false
            });
        }

        // Add protected backups
        // 1. Manual backup from 200 days ago
        string manualPath = Path.Combine(_appPaths.BackupsDirectory, "manual_old.zip");
        File.WriteAllText(manualPath, "manual");
        var manualRec = new BackupRecord
        {
            FileName = "manual_old.zip",
            FilePath = manualPath,
            CreatedAt = now.AddDays(-200),
            SizeBytes = 1024,
            Sha256 = "dummy",
            Kind = BackupKind.Manual,
            Status = BackupStatus.Created,
            IsDeletedLocally = false
        };
        _context.BackupRecords.Add(manualRec);

        // 2. PreRestore backup from 10 days ago (younger than 30 days)
        string preRestorePath = Path.Combine(_appPaths.BackupsDirectory, "prerestore_young.zip");
        File.WriteAllText(preRestorePath, "prerestore");
        var preRestoreRec = new BackupRecord
        {
            FileName = "prerestore_young.zip",
            FilePath = preRestorePath,
            CreatedAt = now.AddDays(-10),
            SizeBytes = 1024,
            Sha256 = "dummy",
            Kind = BackupKind.PreRestore,
            Status = BackupStatus.Created,
            IsDeletedLocally = false
        };
        _context.BackupRecords.Add(preRestoreRec);

        // 3. Backup with Pending Cloud Upload from 150 days ago
        string pendingUploadPath = Path.Combine(_appPaths.BackupsDirectory, "pending_upload.zip");
        File.WriteAllText(pendingUploadPath, "pending");
        var pendingRec = new BackupRecord
        {
            FileName = "pending_upload.zip",
            FilePath = pendingUploadPath,
            CreatedAt = now.AddDays(-150),
            SizeBytes = 1024,
            Sha256 = "dummy",
            Kind = BackupKind.Scheduled,
            Status = BackupStatus.Created,
            DriveStatus = DriveUploadStatus.Pending,
            IsDeletedLocally = false
        };
        _context.BackupRecords.Add(pendingRec);

        // 4. The newest backup (created today)
        string newestPath = Path.Combine(_appPaths.BackupsDirectory, "newest.zip");
        File.WriteAllText(newestPath, "newest");
        var newestRec = new BackupRecord
        {
            FileName = "newest.zip",
            FilePath = newestPath,
            CreatedAt = now,
            SizeBytes = 1024,
            Sha256 = "dummy",
            Kind = BackupKind.Scheduled,
            Status = BackupStatus.Created,
            IsDeletedLocally = false
        };
        _context.BackupRecords.Add(newestRec);

        await _context.SaveChangesAsync();

        // Run Retention
        int deletedCount = await _backupService.RunRetentionAsync();
        deletedCount.Should().BeGreaterThan(300);

        // Verify protected backups are NOT deleted
        var activeBackups = await _context.BackupRecords
            .Where(b => !b.IsDeletedLocally)
            .ToListAsync();

        activeBackups.Any(b => b.Id == newestRec.Id).Should().BeTrue("The newest backup must never be deleted.");
        activeBackups.Any(b => b.Id == manualRec.Id).Should().BeTrue("Manual backups must never be deleted by retention.");
        activeBackups.Any(b => b.Id == preRestoreRec.Id).Should().BeTrue("PreRestore backups younger than 30 days must never be deleted.");
        activeBackups.Any(b => b.Id == pendingRec.Id).Should().BeTrue("Backups with pending cloud uploads must never be deleted.");
    }

    [Fact]
    public async Task DeleteBackups_BlocksDeletingNewestOrPendingUpload()
    {
        var newest = new BackupRecord
        {
            FileName = "newest_backup.zip",
            FilePath = Path.Combine(_appPaths.BackupsDirectory, "newest_backup.zip"),
            CreatedAt = DateTime.UtcNow,
            SizeBytes = 100,
            Sha256 = "abc",
            Kind = BackupKind.Manual,
            Status = BackupStatus.Created,
            IsDeletedLocally = false
        };
        File.WriteAllText(newest.FilePath, "newest");

        var pending = new BackupRecord
        {
            FileName = "pending_backup.zip",
            FilePath = Path.Combine(_appPaths.BackupsDirectory, "pending_backup.zip"),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            SizeBytes = 100,
            Sha256 = "abc",
            Kind = BackupKind.Scheduled,
            Status = BackupStatus.Created,
            DriveStatus = DriveUploadStatus.Uploading,
            IsDeletedLocally = false
        };
        File.WriteAllText(pending.FilePath, "pending");

        _context.BackupRecords.AddRange(newest, pending);
        await _context.SaveChangesAsync();

        var res = await _backupService.DeleteBackupsAsync(new[] { newest.Id, pending.Id });

        res.DeletedCount.Should().Be(0);
        res.BlockedCount.Should().Be(2);
        res.Summary.Should().Contain("newest backup");
    }

    [Fact]
    public async Task RegisterExistingBackups_ScansUntrackedFiles()
    {
        string legacyDb = Path.Combine(_appPaths.BackupsDirectory, "pre-migration-Phase5-20261101.db");
        File.WriteAllText(legacyDb, "legacy-db-content");

        string legacyClear = Path.Combine(_appPaths.BackupsDirectory, "backup-clear-transactions-20261102.db");
        File.WriteAllText(legacyClear, "legacy-clear-content");

        await _backupService.RegisterExistingBackupsAsync();

        var records = await _context.BackupRecords.ToListAsync();
        var migRec = records.FirstOrDefault(r => r.FileName == Path.GetFileName(legacyDb));
        migRec.Should().NotBeNull();
        migRec!.Kind.Should().Be(BackupKind.PreMigration);

        var toolRec = records.FirstOrDefault(r => r.FileName == Path.GetFileName(legacyClear));
        toolRec.Should().NotBeNull();
        toolRec!.Kind.Should().Be(BackupKind.PreDataTool);
    }

    public void Dispose()
    {
        _context.Dispose();
        _serviceProvider.Dispose();
        try { Directory.Delete(_testRoot, recursive: true); } catch { }
    }
}

