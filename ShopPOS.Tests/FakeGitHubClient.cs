using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Tests;

public class FakeGitHubClient : IGitHubClient
{
    public bool RepoExists { get; set; } = true;
    public bool IsPrivate { get; set; } = true;
    public long SizeKb { get; set; } = 1000;
    public string DefaultBranch { get; set; } = "main";
    public bool IsOnline { get; set; } = true;
    public HttpRequestException? ForcedHttpError { get; set; }
    public bool ReadmeCommitted { get; set; } = false;

    public List<string> AccessibleRepos { get; } = new() { "owner/repo" };
    public List<GitHubReleaseDto> Releases { get; } = new();
    public Dictionary<long, List<GitHubReleaseAssetDto>> ReleaseAssets { get; } = new();
    public Dictionary<long, byte[]> AssetContents { get; } = new();

    public List<(long ReleaseId, string Name, byte[] Content, string ContentType)> UploadedAssets { get; } = new();
    public List<long> DeletedAssetIds { get; } = new();
    public List<(string Tag, string Name, bool Prerelease)> CreatedReleases { get; } = new();
    public Dictionary<long, string> ReleaseRepos { get; } = new();

    private long _nextReleaseId = 100;
    private long _nextAssetId = 1000;

    public Task<GitHubRepoInfo> GetRepositoryAsync(string owner, string repo, string? token = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;

        if (!RepoExists)
        {
            return Task.FromResult(new GitHubRepoInfo
            {
                Exists = false,
                StatusCode = 404,
                ErrorMessage = "Not Found"
            });
        }

        return Task.FromResult(new GitHubRepoInfo
        {
            Exists = true,
            IsPrivate = IsPrivate,
            SizeKb = SizeKb,
            DefaultBranch = DefaultBranch,
            StatusCode = 200
        });
    }

    public Task<IReadOnlyList<string>> ListAccessibleRepositoriesAsync(string? token = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;
        return Task.FromResult<IReadOnlyList<string>>(AccessibleRepos.ToList());
    }

    public Task<bool> CheckConnectivityAsync(CancellationToken ct = default)
    {
        return Task.FromResult(IsOnline);
    }

    public Task<bool> EnsureReadmeCommittedAsync(string owner, string repo, string? token = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;
        ReadmeCommitted = true;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<GitHubReleaseDto>> ListReleasesAsync(string owner, string repo, string? token = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;
        string key = $"{owner}/{repo}".ToLowerInvariant();
        var list = Releases.Where(r => !ReleaseRepos.TryGetValue(r.Id, out var k) || k == key).ToList();
        return Task.FromResult<IReadOnlyList<GitHubReleaseDto>>(list);
    }

    public Task<GitHubReleaseDto?> GetReleaseByTagAsync(string owner, string repo, string tag, string? token = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;
        string key = $"{owner}/{repo}".ToLowerInvariant();
        var r = Releases.FirstOrDefault(x => string.Equals(x.TagName, tag, StringComparison.OrdinalIgnoreCase) &&
            (!ReleaseRepos.TryGetValue(x.Id, out var k) || k == key));
        return Task.FromResult(r);
    }

    public Task<GitHubReleaseDto> CreateReleaseAsync(string owner, string repo, string tagName, string name, string body, bool prerelease, string? token = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;

        var release = new GitHubReleaseDto
        {
            Id = ++_nextReleaseId,
            TagName = tagName,
            Name = name,
            Prerelease = prerelease,
            CreatedAt = DateTime.UtcNow
        };
        Releases.Add(release);
        ReleaseRepos[release.Id] = $"{owner}/{repo}".ToLowerInvariant();
        ReleaseAssets[release.Id] = new List<GitHubReleaseAssetDto>();
        CreatedReleases.Add((tagName, name, prerelease));
        return Task.FromResult(release);
    }

    public Task<IReadOnlyList<GitHubReleaseAssetDto>> ListReleaseAssetsAsync(string owner, string repo, long releaseId, string? token = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;

        if (ReleaseAssets.TryGetValue(releaseId, out var list))
        {
            return Task.FromResult<IReadOnlyList<GitHubReleaseAssetDto>>(list.ToList());
        }
        return Task.FromResult<IReadOnlyList<GitHubReleaseAssetDto>>(new List<GitHubReleaseAssetDto>());
    }

    public Task<GitHubReleaseAssetDto> UploadReleaseAssetAsync(string owner, string repo, long releaseId, string assetName, byte[] content, string contentType = "application/octet-stream", string? token = null, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;

        var asset = new GitHubReleaseAssetDto
        {
            Id = ++_nextAssetId,
            Name = assetName,
            Size = content.Length,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            BrowserDownloadUrl = $"https://github.com/{owner}/{repo}/releases/download/mock/{assetName}"
        };

        if (!ReleaseAssets.ContainsKey(releaseId))
        {
            ReleaseAssets[releaseId] = new List<GitHubReleaseAssetDto>();
        }

        // Overwrite or add
        ReleaseAssets[releaseId].RemoveAll(a => string.Equals(a.Name, assetName, StringComparison.OrdinalIgnoreCase));
        ReleaseAssets[releaseId].Add(asset);
        AssetContents[asset.Id] = content;
        UploadedAssets.Add((releaseId, assetName, content, contentType));

        progress?.Report(100);
        return Task.FromResult(asset);
    }

    public Task<bool> DeleteReleaseAssetAsync(string owner, string repo, long assetId, string? token = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;

        DeletedAssetIds.Add(assetId);
        foreach (var kvp in ReleaseAssets)
        {
            kvp.Value.RemoveAll(a => a.Id == assetId);
        }
        AssetContents.Remove(assetId);
        return Task.FromResult(true);
    }

    public Task<byte[]> DownloadReleaseAssetAsync(string owner, string repo, long assetId, string? token = null, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;

        if (AssetContents.TryGetValue(assetId, out var bytes))
        {
            progress?.Report(100);
            return Task.FromResult(bytes);
        }

        throw new HttpRequestException($"Asset {assetId} not found", null, HttpStatusCode.NotFound);
    }

    public Task<string> GetAssetContentAsStringAsync(string owner, string repo, long assetId, string? token = null, CancellationToken ct = default)
    {
        if (ForcedHttpError != null) throw ForcedHttpError;

        if (AssetContents.TryGetValue(assetId, out var bytes))
        {
            return Task.FromResult(Encoding.UTF8.GetString(bytes));
        }

        throw new HttpRequestException($"Asset {assetId} not found", null, HttpStatusCode.NotFound);
    }
}

