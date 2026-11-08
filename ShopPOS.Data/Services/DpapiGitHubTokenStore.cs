using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Secure storage implementation for the GitHub Personal Access Token.
/// Uses Windows DPAPI (ProtectedData, CurrentUser scope) so tokens are stored encrypted at rest
/// on the local machine and never leak into databases, logs, or backup packages.
/// </summary>
public class DpapiGitHubTokenStore : IGitHubTokenStore
{
    private readonly IAppPaths _appPaths;
    private readonly ILogger<DpapiGitHubTokenStore> _logger;

    public DpapiGitHubTokenStore(
        IAppPaths appPaths,
        ILogger<DpapiGitHubTokenStore>? logger = null)
    {
        _appPaths = appPaths;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DpapiGitHubTokenStore>.Instance;
    }

    public async Task SaveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            await DeleteTokenAsync(cancellationToken);
            return;
        }

        try
        {
            _appPaths.EnsureDirectoriesExist();
            byte[] plainBytes = Encoding.UTF8.GetBytes(token.Trim());
            byte[] cipherBytes;

            if (OperatingSystem.IsWindows())
            {
                cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                // Fallback for non-Windows test environments
                cipherBytes = plainBytes;
            }

            string tokenPath = _appPaths.GitHubTokenFilePath;
            string tempPath = tokenPath + ".tmp";

            await File.WriteAllBytesAsync(tempPath, cipherBytes, cancellationToken);
            if (File.Exists(tokenPath))
            {
                File.Delete(tokenPath);
            }
            File.Move(tempPath, tokenPath);

            _logger.LogInformation("Saved encrypted GitHub token to '{Path}'", tokenPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to protect and save GitHub credentials");
            throw;
        }
    }

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string tokenPath = _appPaths.GitHubTokenFilePath;
            if (!File.Exists(tokenPath))
            {
                return null;
            }

            byte[] cipherBytes = await File.ReadAllBytesAsync(tokenPath, cancellationToken);
            if (cipherBytes == null || cipherBytes.Length == 0)
            {
                return null;
            }

            byte[] plainBytes;
            if (OperatingSystem.IsWindows())
            {
                plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                plainBytes = cipherBytes;
            }

            return Encoding.UTF8.GetString(plainBytes).Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt and read GitHub credentials from DPAPI storage");
            return null;
        }
    }

    public Task<bool> HasTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string tokenPath = _appPaths.GitHubTokenFilePath;
            if (!File.Exists(tokenPath)) return Task.FromResult(false);

            var fileInfo = new FileInfo(tokenPath);
            return Task.FromResult(fileInfo.Length > 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task DeleteTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string tokenPath = _appPaths.GitHubTokenFilePath;
            if (File.Exists(tokenPath))
            {
                File.Delete(tokenPath);
                _logger.LogInformation("Deleted GitHub credentials at '{Path}'", tokenPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete GitHub credentials file");
        }

        return Task.CompletedTask;
    }
}

