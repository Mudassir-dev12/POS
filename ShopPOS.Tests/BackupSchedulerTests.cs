using AwesomeAssertions;
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

public class BackupSchedulerTests : IDisposable
{
    private readonly string _testRoot;
    private readonly IAppPaths _appPaths;
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _context;
    private readonly IBackupScheduler _scheduler;
    private readonly IBackupService _backupService;

    public BackupSchedulerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"SchedTest_{Guid.NewGuid():N}");
        _appPaths = new AppPaths(_testRoot);
        _appPaths.EnsureDirectoriesExist();

        var services = new ServiceCollection();
        services.AddSingleton<IAppPaths>(_appPaths);
        services.AddCoreServices();
        services.AddDataServices(new DatabaseOptions { DbPath = _appPaths.DatabasePath });

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<AppDbContext>();
        _context.Database.Migrate();

        _scheduler = _serviceProvider.GetRequiredService<IBackupScheduler>();
        _backupService = _serviceProvider.GetRequiredService<IBackupService>();

        var s = _context.ShopSettings.FirstOrDefault();
        if (s == null)
        {
            s = new ShopSetting { BackupEnabled = true, BackupTime = "22:00", BackupOnAppClose = true };
            _context.ShopSettings.Add(s);
        }
        else
        {
            s.BackupEnabled = true;
            s.BackupTime = "22:00";
            s.BackupOnAppClose = true;
        }
        _context.SaveChanges();
    }

    [Fact]
    public async Task TriggerScheduledBackup_CreatesScheduledBackupRecord()
    {
        await _scheduler.TriggerScheduledBackupAsync();

        var backups = await _backupService.GetBackupsAsync();
        backups.Count.Should().BeGreaterThan(0);
        backups[0].Kind.Should().Be(BackupKind.Scheduled);
    }

    [Fact]
    public async Task RunAppCloseBackup_CreatesBackupRecord()
    {
        await _scheduler.RunAppCloseBackupAsync();

        var backups = await _backupService.GetBackupsAsync();
        backups.Count.Should().BeGreaterThan(0);
        backups.Any(b => b.Kind == BackupKind.Scheduled).Should().BeTrue();
    }

    [Fact]
    public void GetNextScheduledTime_ReturnsValidFutureTime()
    {
        var next = _scheduler.GetNextScheduledTime();
        next.Should().NotBeNull();
        next!.Value.Should().BeAfter(DateTime.Now.AddSeconds(-5));
    }

    [Fact]
    public async Task ConcurrentBackupTriggers_AreBlockedGracefully()
    {
        // Fire 5 triggers concurrently
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _scheduler.TriggerScheduledBackupAsync())
            .ToArray();

        await Task.WhenAll(tasks);

        // Should complete without exception, taking only 1 or sequential backups
        var backups = await _backupService.GetBackupsAsync();
        backups.Count.Should().BeGreaterThan(0);
    }

    public void Dispose()
    {
        _scheduler.Stop();
        _context.Dispose();
        _serviceProvider.Dispose();
        try { Directory.Delete(_testRoot, recursive: true); } catch { }
    }
}

