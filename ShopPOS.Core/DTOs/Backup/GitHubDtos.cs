using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Configuration for the private GitHub repository backup uploader.
/// </summary>
public class GitHubConfigDto
{
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string? Token { get; set; }
    public bool HasToken { get; set; }
    public bool ExcludeImages { get; set; } = true;
    public bool Enabled { get; set; }

    public static GitHubConfigDto Create(string owner, string repo, string branch = "main", bool excludeImages = true, bool enabled = false)
    {
        return new GitHubConfigDto
        {
            Owner = owner,
            Repo = repo,
            Branch = branch,
            ExcludeImages = excludeImages,
            Enabled = enabled
        };
    }
}

/// <summary>
/// Status and health summary of GitHub backups.
/// </summary>
public class GitHubStatusSummaryDto
{
    public bool IsEnabled { get; set; }
    public bool IsConfigured { get; set; }
    public bool HasToken { get; set; }
    public string? Owner { get; set; }
    public string? Repo { get; set; }
    public string Branch { get; set; } = "main";
    public DateTime? LastUploadAt { get; set; }
    public string? LastError { get; set; }
    public bool ReconnectNeeded { get; set; }
    public bool RepoIsPublic { get; set; }
    public bool FileTooLarge { get; set; }
    public int PendingCount { get; set; }
    public long? RepoSizeKb { get; set; }
    public DateTime? LastYearEndArchiveAt { get; set; }

    public string FormattedLastUpload => LastUploadAt.HasValue
        ? LastUploadAt.Value.ToLocalTime().ToString("g")
        : "Never";
}

/// <summary>
/// Represents a backup asset stored on a GitHub release (daily, monthly, or year-end archive).
/// </summary>
public class GitHubBackupItemDto
{
    public long AssetId { get; set; }
    public long ReleaseId { get; set; }
    public string ReleaseTagName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public bool IsMonthly { get; set; }
    public bool IsYearEnd { get; set; }
    public bool IsDaily { get; set; }
    public string? DownloadUrl { get; set; }

    public string FormattedDate => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string FormattedSize => SizeBytes > 0
        ? $"{(double)SizeBytes / (1024 * 1024):F1} MB"
        : "Unknown size";

    public string KindDisplay => IsYearEnd ? "Year-End Archive" : (IsMonthly ? "Monthly Backup" : "Daily Backup");
}

/// <summary>
/// Result of testing GitHub repository connection and access token permissions.
/// </summary>
public class GitHubTestResultDto
{
    public bool Success { get; set; }
    public bool IsPrivate { get; set; }
    public string? RepoFullName { get; set; }
    public string? DefaultBranch { get; set; }
    public int AccessibleRepoCount { get; set; }
    public string? WarningMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Manifest JSON stored as an asset beside each encrypted backup archive on GitHub releases.
/// Contains snapshot metadata for integrity verification without exposing business data.
/// </summary>
public class GitHubManifestDto
{
    public string AppVersion { get; set; } = "1.0.0";
    public string SchemaVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool ExcludedImages { get; set; }
    public bool IsYearEnd { get; set; }
    public int? Year { get; set; }
}

/// <summary>
/// Represents a GitHub release object.
/// </summary>
public class GitHubReleaseDto
{
    public long Id { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Prerelease { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
    public List<GitHubReleaseAssetDto> Assets { get; set; } = new();
}

/// <summary>
/// Represents a GitHub release asset object.
/// </summary>
public class GitHubReleaseAssetDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string BrowserDownloadUrl { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Options for copying backup releases and assets to another repository.
/// </summary>
public class GitHubCopyRepoOptionsDto
{
    public string? SourceToken { get; set; }
    public string? SourceOwner { get; set; }
    public string? SourceRepo { get; set; }
    public string? SourceLocalFolder { get; set; }

    public string DestToken { get; set; } = string.Empty;
    public string DestOwner { get; set; } = string.Empty;
    public string DestRepo { get; set; } = string.Empty;

    public static GitHubCopyRepoOptionsDto FromRepo(string? srcOwner, string? srcRepo, string? srcToken, string destOwner, string destRepo, string destToken)
    {
        return new GitHubCopyRepoOptionsDto
        {
            SourceOwner = srcOwner,
            SourceRepo = srcRepo,
            SourceToken = srcToken,
            DestOwner = destOwner,
            DestRepo = destRepo,
            DestToken = destToken
        };
    }

    public static GitHubCopyRepoOptionsDto FromFolder(string localFolder, string destOwner, string destRepo, string destToken)
    {
        return new GitHubCopyRepoOptionsDto
        {
            SourceLocalFolder = localFolder,
            DestOwner = destOwner,
            DestRepo = destRepo,
            DestToken = destToken
        };
    }
}

/// <summary>
/// Progress reporting during migration or repository replication.
/// </summary>
public class GitHubCopyProgressDto
{
    public string CurrentItem { get; set; } = string.Empty;
    public int CopiedCount { get; set; }
    public int SkippedCount { get; set; }
    public int TotalCount { get; set; }
    public int Percent { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result summary of copying backups to another repository.
/// </summary>
public class GitHubCopyResultDto
{
    public bool Success { get; set; }
    public int TotalFound { get; set; }
    public int CopiedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Messages { get; set; } = new();
}
