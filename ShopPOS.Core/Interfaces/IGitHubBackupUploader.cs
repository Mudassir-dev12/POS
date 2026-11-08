using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShopPOS.Core.DTOs.Backup;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Specialized contract for GitHub private repository backup and restore using GitHub Release Assets.
/// Also implements the uploader-agnostic IBackupUploader contract.
/// </summary>
public interface IGitHubBackupUploader : IBackupUploader
{
    /// <summary>
    /// Tests connectivity, repository visibility, and fine-grained permissions for given credentials.
    /// </summary>
    Task<GitHubTestResultDto> TestConnectionAsync(string owner, string repo, string branch, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures or updates the GitHub backup settings and safely stores the token in DPAPI.
    /// </summary>
    Task<bool> ConfigureAsync(GitHubConfigDto config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns high-level GitHub sync status and metadata for UI dashboard and backup view.
    /// </summary>
    Task<GitHubStatusSummaryDto> GetGitHubStatusSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares an empty or initialized repository by committing a bare README.md. No business data is committed.
    /// </summary>
    Task<bool> PrepareRepositoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available backup items on GitHub: this year's daily files, monthly archives, and past year-end archives.
    /// </summary>
    Task<IReadOnlyList<GitHubBackupItemDto>> ListGitHubBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a backup release asset by asset ID or filename, verifies SHA-256 against its manifest,
    /// and registers it as a local BackupRecord ready for restore.
    /// </summary>
    Task<string> DownloadBackupFromGitHubAsync(string assetIdOrName, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and uploads a Year-End archive (ShopPOS_<slug>_<yyyy>_YEAR-END.zip.enc) containing the last backup
    /// of that year plus encrypted CSV exports for sales, purchases, expenses, stock movements, and adjustments.
    /// </summary>
    Task<bool> CreateYearEndArchiveAsync(int? targetYear = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies all backup releases and assets from a source repository or local directory to a destination repository.
    /// Verifies SHA-256, skips identical assets, never deletes from the source.
    /// </summary>
    Task<GitHubCopyResultDto> CopyBackupsToAnotherRepositoryAsync(GitHubCopyRepoOptionsDto options, IProgress<GitHubCopyProgressDto>? progress = null, CancellationToken cancellationToken = default);
}
