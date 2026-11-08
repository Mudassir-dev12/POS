using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Direct HTTPS GitHub REST and Release Assets API client.
/// Works with fine-grained personal access tokens, performs no local git operations,
/// uses uploads.github.com for release assets, and leaks no tokens to logs or persistence.
/// </summary>
public class GitHubClient : IGitHubClient
{
    private readonly HttpClient _httpClient;
    private readonly IGitHubTokenStore _tokenStore;
    private readonly ILogger<GitHubClient> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GitHubClient(
        HttpClient httpClient,
        IGitHubTokenStore tokenStore,
        ILogger<GitHubClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubClient>.Instance;

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.github.com/");
        }

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ShopPOS-BackupUploader");
        }
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string requestUri, string? tokenOverride = null, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        string? token = tokenOverride;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = await _tokenStore.GetTokenAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    public async Task<GitHubRepoInfo> GetRepositoryAsync(string owner, string repo, string? token = null, CancellationToken ct = default)
    {
        var result = new GitHubRepoInfo();
        try
        {
            using var req = await CreateRequestAsync(HttpMethod.Get, $"repos/{owner}/{repo}", token, ct);
            using var res = await _httpClient.SendAsync(req, ct);

            result.StatusCode = (int)res.StatusCode;
            if (!res.IsSuccessStatusCode)
            {
                result.Exists = false;
                result.ErrorMessage = $"GitHub API returned {(int)res.StatusCode} ({res.ReasonPhrase})";
                return result;
            }

            result.Exists = true;
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("private", out var privProp))
            {
                result.IsPrivate = privProp.GetBoolean();
            }

            if (root.TryGetProperty("size", out var sizeProp))
            {
                result.SizeKb = sizeProp.GetInt64();
            }

            if (root.TryGetProperty("default_branch", out var branchProp))
            {
                result.DefaultBranch = branchProp.GetString() ?? "main";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inspect repository '{Owner}/{Repo}'", owner, repo);
            result.StatusCode = 0;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async Task<IReadOnlyList<string>> ListAccessibleRepositoriesAsync(string? token = null, CancellationToken ct = default)
    {
        var list = new List<string>();
        try
        {
            using var req = await CreateRequestAsync(HttpMethod.Get, "user/repos?per_page=100&affiliation=owner,collaborator,organization_member", token, ct);
            using var res = await _httpClient.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                return list;
            }

            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("full_name", out var fn))
                    {
                        var name = fn.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            list.Add(name);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not list accessible repositories for scope check");
        }

        return list;
    }

    public async Task<bool> CheckConnectivityAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "zen");
            req.Headers.Add("User-Agent", "ShopPOS-BackupUploader");
            using var res = await _httpClient.SendAsync(req, ct);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EnsureReadmeCommittedAsync(string owner, string repo, string? token = null, CancellationToken ct = default)
    {
        try
        {
            // Check if README.md already exists
            using (var checkReq = await CreateRequestAsync(HttpMethod.Get, $"repos/{owner}/{repo}/contents/README.md", token, ct))
            {
                using var checkRes = await _httpClient.SendAsync(checkReq, ct);
                if (checkRes.IsSuccessStatusCode)
                {
                    return true; // Already exists
                }
            }

            // Create initial bare README.md (no business data)
            string readmeContent = "# ShopPOS Backups\nThis repository stores encrypted backup release assets for ShopPOS.\n";
            string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(readmeContent));

            using (var putReq = await CreateRequestAsync(HttpMethod.Put, $"repos/{owner}/{repo}/contents/README.md", token, ct))
            {
                var payload = new
                {
                    message = "Initialize ShopPOS Backup Repository",
                    content = base64
                };
                putReq.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var putRes = await _httpClient.SendAsync(putReq, ct);
                return putRes.IsSuccessStatusCode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure README.md was committed");
            return false;
        }
    }

    public async Task<IReadOnlyList<GitHubReleaseDto>> ListReleasesAsync(string owner, string repo, string? token = null, CancellationToken ct = default)
    {
        var list = new List<GitHubReleaseDto>();
        try
        {
            using var req = await CreateRequestAsync(HttpMethod.Get, $"repos/{owner}/{repo}/releases?per_page=100", token, ct);
            using var res = await _httpClient.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                return list;
            }

            var json = await res.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<List<GitHubReleaseDto>>(json, _jsonOptions);
            return parsed ?? list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list releases for {Owner}/{Repo}", owner, repo);
            return list;
        }
    }

    public async Task<GitHubReleaseDto?> GetReleaseByTagAsync(string owner, string repo, string tag, string? token = null, CancellationToken ct = default)
    {
        try
        {
            using var req = await CreateRequestAsync(HttpMethod.Get, $"repos/{owner}/{repo}/releases/tags/{tag}", token, ct);
            using var res = await _httpClient.SendAsync(req, ct);

            if (res.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"Failed to get release by tag '{tag}': {(int)res.StatusCode} {res.ReasonPhrase}. {err}", null, res.StatusCode);
            }

            var json = await res.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<GitHubReleaseDto>(json, _jsonOptions);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting release for tag {Tag}", tag);
            throw;
        }
    }

    public async Task<GitHubReleaseDto> CreateReleaseAsync(string owner, string repo, string tagName, string name, string body, bool prerelease, string? token = null, CancellationToken ct = default)
    {
        using var req = await CreateRequestAsync(HttpMethod.Post, $"repos/{owner}/{repo}/releases", token, ct);
        var payload = new
        {
            tag_name = tagName,
            name = name,
            body = body,
            draft = false,
            prerelease = prerelease
        };
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var res = await _httpClient.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to create release '{tagName}': {(int)res.StatusCode} {res.ReasonPhrase}. {err}", null, res.StatusCode);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<GitHubReleaseDto>(json, _jsonOptions)!;
    }

    public async Task<IReadOnlyList<GitHubReleaseAssetDto>> ListReleaseAssetsAsync(string owner, string repo, long releaseId, string? token = null, CancellationToken ct = default)
    {
        var list = new List<GitHubReleaseAssetDto>();
        try
        {
            using var req = await CreateRequestAsync(HttpMethod.Get, $"repos/{owner}/{repo}/releases/{releaseId}/assets?per_page=100", token, ct);
            using var res = await _httpClient.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                return list;
            }

            var json = await res.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<List<GitHubReleaseAssetDto>>(json, _jsonOptions) ?? list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list assets for release {ReleaseId}", releaseId);
            return list;
        }
    }

    public async Task<GitHubReleaseAssetDto> UploadReleaseAssetAsync(string owner, string repo, long releaseId, string assetName, byte[] content, string contentType = "application/octet-stream", string? token = null, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        string uploadUri = $"https://uploads.github.com/repos/{owner}/{repo}/releases/{releaseId}/assets?name={Uri.EscapeDataString(assetName)}";

        using var req = new HttpRequestMessage(HttpMethod.Post, uploadUri);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        string? activeToken = token;
        if (string.IsNullOrWhiteSpace(activeToken))
        {
            activeToken = await _tokenStore.GetTokenAsync(ct);
        }
        if (!string.IsNullOrWhiteSpace(activeToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activeToken);
        }

        req.Content = new ByteArrayContent(content);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        req.Content.Headers.ContentLength = content.Length;

        using var res = await _httpClient.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to upload asset '{assetName}': {(int)res.StatusCode} {res.ReasonPhrase}. {err}", null, res.StatusCode);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<GitHubReleaseAssetDto>(json, _jsonOptions)!;
    }

    public async Task<bool> DeleteReleaseAssetAsync(string owner, string repo, long assetId, string? token = null, CancellationToken ct = default)
    {
        try
        {
            using var req = await CreateRequestAsync(HttpMethod.Delete, $"repos/{owner}/{repo}/releases/assets/{assetId}", token, ct);
            using var res = await _httpClient.SendAsync(req, ct);
            return res.IsSuccessStatusCode || res.StatusCode == HttpStatusCode.NotFound;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete release asset {AssetId}", assetId);
            return false;
        }
    }

    public async Task<byte[]> DownloadReleaseAssetAsync(string owner, string repo, long assetId, string? token = null, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        using var req = await CreateRequestAsync(HttpMethod.Get, $"repos/{owner}/{repo}/releases/assets/{assetId}", token, ct);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

        using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        // If redirected (302) to S3/CDN or direct stream:
        HttpResponseMessage finalRes = res;
        if (res.StatusCode == HttpStatusCode.Found || res.StatusCode == HttpStatusCode.SeeOther || res.StatusCode == HttpStatusCode.TemporaryRedirect)
        {
            var redirectUrl = res.Headers.Location;
            if (redirectUrl != null)
            {
                using var redirectReq = new HttpRequestMessage(HttpMethod.Get, redirectUrl);
                finalRes = await _httpClient.SendAsync(redirectReq, HttpCompletionOption.ResponseHeadersRead, ct);
            }
        }

        if (!finalRes.IsSuccessStatusCode)
        {
            var err = await finalRes.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to download release asset {assetId}: {(int)finalRes.StatusCode} {finalRes.ReasonPhrase}. {err}", null, finalRes.StatusCode);
        }

        long? totalBytes = finalRes.Content.Headers.ContentLength;
        using var stream = await finalRes.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();

        byte[] buffer = new byte[81920];
        long readSoFar = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await ms.WriteAsync(buffer, 0, bytesRead, ct);
            readSoFar += bytesRead;

            if (totalBytes.HasValue && totalBytes.Value > 0 && progress != null)
            {
                int pct = (int)((readSoFar * 100) / totalBytes.Value);
                progress.Report(pct);
            }
        }

        return ms.ToArray();
    }

    public async Task<string> GetAssetContentAsStringAsync(string owner, string repo, long assetId, string? token = null, CancellationToken ct = default)
    {
        byte[] bytes = await DownloadReleaseAssetAsync(owner, repo, assetId, token, null, ct);
        return Encoding.UTF8.GetString(bytes);
    }
}
