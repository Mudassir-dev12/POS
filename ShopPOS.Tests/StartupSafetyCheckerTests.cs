using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.Interfaces;
using ShopPOS.Data;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class StartupSafetyCheckerTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly StartupSafetyChecker _checker;

    public StartupSafetyCheckerTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "ShopPOS_StartupCheck_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
        _checker = new StartupSafetyChecker();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void ValidateFileSystem_ValidWritableDirectory_ReturnsSuccess()
    {
        var result = _checker.ValidateFileSystem(_tempFolder);

        Assert.True(result.CanProceed);
        Assert.Null(result.Message);
    }

    [Fact]
    public void ValidateFileSystem_NonExistentInvalidPath_FailsGracefully()
    {
        string invalidPath = "Z:\\NonExistent_Invalid_Dir_12345\\Foo\\Bar";

        var result = _checker.ValidateFileSystem(invalidPath);

        Assert.False(result.CanProceed);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public void CheckDatabaseLock_WhenNotLocked_ReturnsCanProceed()
    {
        string dbPath = Path.Combine(_tempFolder, "unlocked.db");
        File.WriteAllText(dbPath, "test db content");

        var result = _checker.CheckDatabaseLock(dbPath);

        Assert.True(result.CanProceed);
        Assert.Equal(StartupIssueType.None, result.Issue);
    }

    [Fact]
    public void CheckDatabaseLock_WhenExclusivelyLocked_DetectsLock()
    {
        string dbPath = Path.Combine(_tempFolder, "locked.db");
        File.WriteAllText(dbPath, "test content");

        using (var fs = new FileStream(dbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = _checker.CheckDatabaseLock(dbPath);

            Assert.False(result.CanProceed);
            Assert.Equal(StartupIssueType.DatabaseLocked, result.Issue);
            Assert.Contains("locked by another", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ValidateDatabaseAsync_ValidMigratedDb_ReturnsSuccess()
    {
        string dbPath = Path.Combine(_tempFolder, "migrated.db");
        string connStr = $"Data Source={dbPath};Foreign Keys=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connStr)
            .Options;

        using (var ctx = new AppDbContext(options))
        {
            await ctx.Database.MigrateAsync();
        }

        var result = await _checker.ValidateDatabaseAsync(dbPath);

        Assert.True(result.CanProceed);
        Assert.False(result.NeedsRestorePrompt);
    }

    [Fact]
    public async Task ValidateDatabaseAsync_CorruptedDbFile_DetectsCorruption()
    {
        string dbPath = Path.Combine(_tempFolder, "corrupt.db");
        File.WriteAllBytes(dbPath, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x55, 0xAA, 0xFF, 0xEE });

        var result = await _checker.ValidateDatabaseAsync(dbPath);

        Assert.False(result.CanProceed);
        Assert.True(result.NeedsRestorePrompt);
        Assert.Equal(StartupIssueType.DatabaseCorrupted, result.Issue);
    }
}
