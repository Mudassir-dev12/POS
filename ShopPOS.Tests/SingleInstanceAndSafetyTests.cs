using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShopPOS.Core;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class SingleInstanceAndSafetyTests : IDisposable
{
    private readonly string _testRoot;
    private readonly IAppPaths _appPaths;
    private readonly ServiceProvider _serviceProvider;
    private readonly IDatabaseSafetyService _safetyService;

    public SingleInstanceAndSafetyTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"SafetyTest_{Guid.NewGuid():N}");
        _appPaths = new AppPaths(_testRoot);
        _appPaths.EnsureDirectoriesExist();

        var services = new ServiceCollection();
        services.AddSingleton<IAppPaths>(_appPaths);
        services.AddCoreServices();
        services.AddDataServices(new DatabaseOptions { DbPath = _appPaths.DatabasePath });

        _serviceProvider = services.BuildServiceProvider();
        var context = _serviceProvider.GetRequiredService<AppDbContext>();
        context.Database.Migrate();

        _safetyService = _serviceProvider.GetRequiredService<IDatabaseSafetyService>();
    }

    [Fact]
    public void LockFile_DetectsUncleanShutdown_AndRemovesOnCleanExit()
    {
        // First run on clean state: lock file shouldn't exist initially
        if (File.Exists(_appPaths.LockFilePath)) File.Delete(_appPaths.LockFilePath);

        bool wasUnclean1 = _safetyService.CheckAndCreateLockFile();
        wasUnclean1.Should().BeFalse();
        File.Exists(_appPaths.LockFilePath).Should().BeTrue();

        // Simulate crash by keeping the lock file and starting a new run
        bool wasUnclean2 = _safetyService.CheckAndCreateLockFile();
        wasUnclean2.Should().BeTrue("Lock file already existed from previous run, indicating unclean shutdown.");

        // Clean exit removes lock file
        _safetyService.RemoveLockFile();
        File.Exists(_appPaths.LockFilePath).Should().BeFalse();
    }

    [Fact]
    public void PurgeOldLogs_DeletesLogsOlderThan30Days_AndKeepsNewer()
    {
        string logsDir = _appPaths.LogsDirectory;
        Directory.CreateDirectory(logsDir);

        string oldLog = Path.Combine(logsDir, "shoppos-old.log");
        File.WriteAllText(oldLog, "old log content");
        File.SetLastWriteTimeUtc(oldLog, DateTime.UtcNow.AddDays(-35));

        string newLog = Path.Combine(logsDir, "shoppos-new.log");
        File.WriteAllText(newLog, "recent log content");
        File.SetLastWriteTimeUtc(newLog, DateTime.UtcNow.AddDays(-5));

        int deleted = _safetyService.PurgeOldLogs(30);

        deleted.Should().Be(1);
        File.Exists(oldLog).Should().BeFalse();
        File.Exists(newLog).Should().BeTrue();
    }

    [Fact]
    public async Task QuickCheck_OnHealthyDatabase_ReturnsTrue()
    {
        bool healthy = await _safetyService.RunQuickCheckAsync();
        healthy.Should().BeTrue();
    }

    [Fact]
    public async Task WalCheckpoint_ExecutesWithoutError()
    {
        Func<Task> act = async () => await _safetyService.RunWalCheckpointAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void SingleInstanceMutex_DetectsDuplicateInstance()
    {
        string uniqueMutexName = $"Test_ShopPOS_Mutex_{Guid.NewGuid():N}";

        using var mutex1 = new Mutex(true, uniqueMutexName, out bool createdNew1);
        createdNew1.Should().BeTrue();

        using var mutex2 = new Mutex(true, uniqueMutexName, out bool createdNew2);
        createdNew2.Should().BeFalse("Second instance must detect that mutex already exists.");
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        try { Directory.Delete(_testRoot, recursive: true); } catch { }
    }
}

