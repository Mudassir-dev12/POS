using System.IO.Compression;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

public class RestoreServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly IAppPaths _appPaths;
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _context;
    private readonly IBackupService _backupService;
    private readonly IRestoreService _restoreService;

    public RestoreServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"RestoreTest_{Guid.NewGuid():N}");
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
        _restoreService = _serviceProvider.GetRequiredService<IRestoreService>();

        var category = new Category { Name = "General" };
        var unit = new Unit { Name = "Piece", ShortName = "pc" };
        _context.Categories.Add(category);
        _context.Units.Add(unit);
        _context.SaveChanges();

        // Seed initial data
        _context.Products.Add(new Product
        {
            Name = "Initial Product A",
            Sku = "INIT-001",
            CostPrice = 100,
            SalePrice = 200,
            CategoryId = category.Id,
            UnitId = unit.Id
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task RestoreBackup_ValidArchive_ReplacesDatabaseAndCreatesPreRestoreSafetyBackup()
    {
        // 1. Create a backup with known state (1 product)
        var backupResult = await _backupService.CreateBackupAsync(BackupKind.Manual);
        backupResult.Success.Should().BeTrue();
        string backupFile = backupResult.FilePath!;

        // 2. Modify live database state (add more products, delete initial)
        _context.Products.Add(new Product
        {
            Name = "Post-Backup Product B",
            Sku = "POST-002",
            CostPrice = 300,
            SalePrice = 500,
            CategoryId = 1,
            UnitId = 1
        });
        await _context.SaveChangesAsync();
        (await _context.Products.CountAsync()).Should().Be(2);

        // 3. Perform Restore from earlier backup
        await _context.Database.CloseConnectionAsync();
        var restoreResult = await _restoreService.RestoreBackupAsync(backupFile);

        restoreResult.Success.Should().BeTrue();
        restoreResult.PreRestoreBackupFileName.Should().NotBeNullOrWhiteSpace();

        // Verify PreRestore backup actually exists in Backups folder
        string preRestorePath = Path.Combine(_appPaths.BackupsDirectory, restoreResult.PreRestoreBackupFileName!);
        File.Exists(preRestorePath).Should().BeTrue();

        // 4. Verify database state after restore (using fresh scope to reload db connection)
        using var newScope = _serviceProvider.CreateScope();
        var verifyContext = newScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var productsAfter = await verifyContext.Products.ToListAsync();
        productsAfter.Count.Should().Be(1);
        productsAfter[0].Sku.Should().Be("INIT-001");
    }

    [Fact]
    public async Task ValidateBackup_NewerSchemaVersion_RefusesAndInstructsUserToUpdate()
    {
        // Create a valid backup file
        var backup = await _backupService.CreateBackupAsync(BackupKind.Manual);
        backup.Success.Should().BeTrue();

        // Unpack zip, modify manifest with a future schema version, and repack
        string tempZip = Path.Combine(_appPaths.TempDirectory, "future_schema.zip");
        File.Copy(backup.FilePath!, tempZip, true);

        using (var archive = ZipFile.Open(tempZip, ZipArchiveMode.Update))
        {
            var manifestEntry = archive.GetEntry("manifest.json");
            manifestEntry!.Delete();

            var newEntry = archive.CreateEntry("manifest.json");
            var futureManifest = new BackupManifest
            {
                AppVersion = "99.0.0",
                SchemaVersion = "20991231_Phase99_FutureChanges",
                CreatedAt = DateTime.UtcNow,
                MachineName = "FUTURE-PC",
                ShopName = "Future Shop",
                TableRowCounts = new Dictionary<string, int> { ["Products"] = 1 }
            };

            using var stream = newEntry.Open();
            await JsonSerializer.SerializeAsync(stream, futureManifest);
        }

        var validation = await _restoreService.ValidateBackupForRestoreAsync(tempZip);

        validation.IsValid.Should().BeFalse();
        validation.SchemaIsNewerThanApp.Should().BeTrue();
        validation.ErrorMessage.Should().Contain("newer version of ShopPOS");
    }

    [Fact]
    public async Task RestoreBackup_EncryptedBackup_WrongPasswordFailsCleanly()
    {
        string password = "SecretMasterPassword123!";
        var backup = await _backupService.CreateBackupAsync(BackupKind.Manual, password);
        backup.Success.Should().BeTrue();

        var val = await _restoreService.ValidateBackupForRestoreAsync(backup.FilePath!, "WrongPassword!");
        val.IsValid.Should().BeFalse();
        val.ErrorMessage.Should().Contain("password");
    }

    [Fact]
    public async Task RestoreBackup_CorruptedFile_LeavesOriginalDatabaseIntact()
    {
        int initialCount = await _context.Products.CountAsync();

        string corruptFile = Path.Combine(_appPaths.TempDirectory, "bad.zip");
        File.WriteAllText(corruptFile, "Not a zip archive");

        var result = await _restoreService.RestoreBackupAsync(corruptFile);

        result.Success.Should().BeFalse();

        // Verify original database was completely untouched
        using var newScope = _serviceProvider.CreateScope();
        var verifyContext = newScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyContext.Products.CountAsync()).Should().Be(initialCount);
    }

    public void Dispose()
    {
        _context.Dispose();
        _serviceProvider.Dispose();
        try { Directory.Delete(_testRoot, recursive: true); } catch { }
    }
}

