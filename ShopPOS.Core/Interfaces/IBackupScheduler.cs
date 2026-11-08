namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Background scheduling service managing automated daily backups, catch-up backups, retry rules, and app-closing backups.
/// </summary>
public interface IBackupScheduler
{
    /// <summary>
    /// Starts the background scheduler loop.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the background scheduler loop.
    /// </summary>
    void Stop();

    /// <summary>
    /// Triggers an immediate scheduled backup if not already running.
    /// </summary>
    Task TriggerScheduledBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a closing backup when the application shuts down, respecting the configured timeout.
    /// </summary>
    Task RunAppCloseBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the next scheduled backup time according to settings.
    /// </summary>
    DateTime? GetNextScheduledTime();

    /// <summary>
    /// Gets whether a backup or restore operation is currently in progress.
    /// </summary>
    bool IsBusy { get; }
}

