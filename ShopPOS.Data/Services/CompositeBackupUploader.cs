using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Composite uploader orchestrating Google Drive and/or GitHub backup uploaders
/// based on active shop configuration.
/// </summary>
public class CompositeBackupUploader : IBackupUploader
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGitHubBackupUploader _gitHubUploader;
    private readonly IBackupUploader _driveUploader;

    public bool IsQueueBusy => _gitHubUploader.IsQueueBusy || _driveUploader.IsQueueBusy;

    public CompositeBackupUploader(
        IServiceScopeFactory scopeFactory,
        IGitHubBackupUploader gitHubUploader,
        BackupUploader driveUploader)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _gitHubUploader = gitHubUploader ?? throw new ArgumentNullException(nameof(gitHubUploader));
        _driveUploader = driveUploader ?? throw new ArgumentNullException(nameof(driveUploader));
    }

    public async Task<bool> ConnectAsync(Func<string, Task>? onBrowserOpen = null, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (settings?.GitHubEnabled == true)
        {
            return await _gitHubUploader.ConnectAsync(onBrowserOpen, cancellationToken);
        }

        return await _driveUploader.ConnectAsync(onBrowserOpen, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (settings?.GitHubEnabled == true)
        {
            await _gitHubUploader.DisconnectAsync(cancellationToken);
        }
        if (settings?.GoogleDriveEnabled == true)
        {
            await _driveUploader.DisconnectAsync(cancellationToken);
        }
    }

    public async Task<DriveStatusSummaryDto> GetStatusSummaryAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (settings?.GitHubEnabled == true)
        {
            return await _gitHubUploader.GetStatusSummaryAsync(cancellationToken);
        }

        return await _driveUploader.GetStatusSummaryAsync(cancellationToken);
    }

    public async Task ProcessQueueAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (settings == null) return;

        if (settings.GitHubEnabled)
        {
            await _gitHubUploader.ProcessQueueAsync(force, cancellationToken);
        }

        if (settings.GoogleDriveEnabled)
        {
            await _driveUploader.ProcessQueueAsync(force, cancellationToken);
        }
    }

    public async Task<bool> UploadSingleBackupAsync(int backupRecordId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        bool success = false;
        if (settings?.GitHubEnabled == true)
        {
            success = await _gitHubUploader.UploadSingleBackupAsync(backupRecordId, cancellationToken);
        }
        else if (settings?.GoogleDriveEnabled == true)
        {
            success = await _driveUploader.UploadSingleBackupAsync(backupRecordId, cancellationToken);
        }
        else
        {
            // Default to GitHub uploader if neither explicitly turned on
            success = await _gitHubUploader.UploadSingleBackupAsync(backupRecordId, cancellationToken);
        }

        return success;
    }

    public async Task<IReadOnlyList<DriveBackupItemDto>> ListDriveBackupsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (settings?.GitHubEnabled == true)
        {
            return await _gitHubUploader.ListDriveBackupsAsync(cancellationToken);
        }

        return await _driveUploader.ListDriveBackupsAsync(cancellationToken);
    }

    public async Task<string> DownloadBackupFromDriveAsync(string driveFileId, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (settings?.GitHubEnabled == true)
        {
            return await _gitHubUploader.DownloadBackupFromDriveAsync(driveFileId, progress, cancellationToken);
        }

        return await _driveUploader.DownloadBackupFromDriveAsync(driveFileId, progress, cancellationToken);
    }

    public async Task<bool> CheckInternetAccessAsync(CancellationToken cancellationToken = default)
    {
        return await _gitHubUploader.CheckInternetAccessAsync(cancellationToken);
    }
}

