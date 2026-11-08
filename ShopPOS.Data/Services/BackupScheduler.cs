using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Background scheduler managing automated daily backups, catch-up backups, retry rules, and app-closing backups.
/// </summary>
public class BackupScheduler : IBackupScheduler, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupScheduler> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Timer? _timer;
    private bool _isStarted;
    private bool _catchUpExecuted;
    private int _consecutiveFailures;
    private DateTime? _nextRetryAt;
    private string? _lastExecutedDate;

    public bool IsBusy => _lock.CurrentCount == 0;

    public BackupScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<BackupScheduler>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupScheduler>.Instance;
    }

    public void Start()
    {
        if (_isStarted) return;
        _isStarted = true;

        // Check every 30 seconds
        _timer = new Timer(async _ => await CheckScheduleLoopAsync(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30));

        // Start delayed catch-up check after 2 minutes so application startup stays fast
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2));
                await RunStartupCatchUpCheckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error executing startup backup catch-up check.");
            }
        });

        _logger.LogInformation("Backup scheduler started.");
    }

    public void Stop()
    {
        if (!_isStarted) return;
        _isStarted = false;
        _timer?.Change(Timeout.Infinite, 0);
        _timer?.Dispose();
        _timer = null;
        _logger.LogInformation("Backup scheduler stopped.");
    }

    public DateTime? GetNextScheduledTime()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = db.ShopSettings.AsNoTracking().FirstOrDefault();
        if (settings == null || !settings.BackupEnabled) return null;

        if (TimeSpan.TryParse(settings.BackupTime, out var timeOfDay))
        {
            var now = DateTime.Now;
            var candidate = now.Date.Add(timeOfDay);
            if (candidate <= now) candidate = candidate.AddDays(1);
            return candidate;
        }

        return null;
    }

    public async Task TriggerScheduledBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!await _lock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Backup already running; skipping trigger.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var settingsDto = await backupService.GetSettingsAsync(cancellationToken);

            if (!settingsDto.BackupEnabled)
            {
                _logger.LogInformation("Automatic backups are disabled; skipping.");
                return;
            }

            _logger.LogInformation("Starting scheduled daily backup...");
            var result = await backupService.CreateBackupAsync(BackupKind.Scheduled, cancellationToken: cancellationToken);

            if (result.Success)
            {
                _consecutiveFailures = 0;
                _nextRetryAt = null;
                _lastExecutedDate = DateTime.Now.ToString("yyyy-MM-dd");
                _logger.LogInformation("Scheduled backup completed successfully: {FileName}", result.FileName);
            }
            else
            {
                _consecutiveFailures++;
                _logger.LogWarning("Scheduled backup failed (Attempt {Attempt}/3): {Error}", _consecutiveFailures, result.ErrorMessage);

                if (_consecutiveFailures < 3)
                {
                    _nextRetryAt = DateTime.Now.AddMinutes(30);
                    _logger.LogInformation("Scheduled backup retry scheduled for {RetryTime}", _nextRetryAt);
                }
                else
                {
                    _nextRetryAt = null;
                    _logger.LogError("Scheduled backup failed 3 times. Waiting for next daily scheduled time.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error running scheduled backup.");
            _consecutiveFailures++;
            if (_consecutiveFailures < 3)
            {
                _nextRetryAt = DateTime.Now.AddMinutes(30);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RunAppCloseBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!await _lock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Backup or restore in progress; skipping closing backup.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var settings = await backupService.GetSettingsAsync(cancellationToken);

            if (!settings.BackupEnabled || !settings.BackupOnAppClose)
            {
                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(20)); // 20 second timeout limit

            _logger.LogInformation("Running application close backup...");
            var res = await backupService.CreateBackupAsync(BackupKind.Scheduled, cancellationToken: cts.Token);
            if (res.Success)
            {
                _logger.LogInformation("Application close backup completed successfully: {FileName}", res.FileName);
            }
            else
            {
                _logger.LogWarning("Application close backup failed: {Error}", res.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Application close backup exceeded 20-second timeout and was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running application close backup.");
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task CheckScheduleLoopAsync()
    {
        if (!_isStarted || IsBusy) return;

        // Check if retry is due
        if (_nextRetryAt.HasValue && DateTime.Now >= _nextRetryAt.Value)
        {
            _nextRetryAt = null;
            await TriggerScheduledBackupAsync();
            return;
        }

        // Check if daily schedule matches
        var now = DateTime.Now;
        string todayStr = now.ToString("yyyy-MM-dd");

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync();
            if (settings == null || !settings.BackupEnabled) return;

            if (TimeSpan.TryParse(settings.BackupTime, out var targetTime))
            {
                var targetDateTime = now.Date.Add(targetTime);
                // Trigger if within a 1-minute window and not yet executed today
                if (Math.Abs((now - targetDateTime).TotalSeconds) <= 45 && _lastExecutedDate != todayStr)
                {
                    _lastExecutedDate = todayStr;
                    await TriggerScheduledBackupAsync();
                }
            }
        }
    }

    private async Task RunStartupCatchUpCheckAsync()
    {
        if (_catchUpExecuted || !_isStarted) return;
        _catchUpExecuted = true;

        using var scope = _scopeFactory.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
        var settings = await backupService.GetSettingsAsync();

        if (!settings.BackupEnabled) return;

        bool needsCatchUp = false;
        if (!settings.LastSuccessfulBackupAt.HasValue)
        {
            needsCatchUp = true;
        }
        else if (DateTime.UtcNow - settings.LastSuccessfulBackupAt.Value > TimeSpan.FromHours(24))
        {
            needsCatchUp = true;
        }

        if (needsCatchUp)
        {
            _logger.LogInformation("Last successful backup is older than 24 hours (or none exists). Running startup catch-up backup...");
            await TriggerScheduledBackupAsync();
        }
    }

    public void Dispose()
    {
        Stop();
        _lock.Dispose();
    }
}
