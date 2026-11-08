using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShopPOS.Core.DTOs.Backup;

namespace ShopPOS.Core.Interfaces;

public class GitHubRepoInfo
{
    public bool Exists { get; set; }
    public bool IsPrivate { get; set; }
    public long SizeKb { get; set; }
    public string DefaultBranch { get; set; } = "main";
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IGitHubClient
{
    Task<GitHubRepoInfo> GetRepositoryAsync(string owner, string repo, string? token = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListAccessibleRepositoriesAsync(string? token = null, CancellationToken ct = default);
    Task<bool> CheckConnectivityAsync(CancellationToken ct = default);

    /// <summary>
    /// Prepares a new or empty repository by committing an initial README.md. Contains no business data.
    /// </summary>
    Task<bool> EnsureReadmeCommittedAsync(string owner, string repo, string? token = null, CancellationToken ct = default);

    // Release Management
    Task<IReadOnlyList<GitHubReleaseDto>> ListReleasesAsync(string owner, string repo, string? token = null, CancellationToken ct = default);
    Task<GitHubReleaseDto?> GetReleaseByTagAsync(string owner, string repo, string tag, string? token = null, CancellationToken ct = default);
    Task<GitHubReleaseDto> CreateReleaseAsync(string owner, string repo, string tagName, string name, string body, bool prerelease, string? token = null, CancellationToken ct = default);

    // Asset Management (via uploads.github.com)
    Task<IReadOnlyList<GitHubReleaseAssetDto>> ListReleaseAssetsAsync(string owner, string repo, long releaseId, string? token = null, CancellationToken ct = default);
    Task<GitHubReleaseAssetDto> UploadReleaseAssetAsync(string owner, string repo, long releaseId, string assetName, byte[] content, string contentType = "application/octet-stream", string? token = null, IProgress<int>? progress = null, CancellationToken ct = default);
    Task<bool> DeleteReleaseAssetAsync(string owner, string repo, long assetId, string? token = null, CancellationToken ct = default);
    Task<byte[]> DownloadReleaseAssetAsync(string owner, string repo, long assetId, string? token = null, IProgress<int>? progress = null, CancellationToken ct = default);
    Task<string> GetAssetContentAsStringAsync(string owner, string repo, long assetId, string? token = null, CancellationToken ct = default);
}
