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
/// Secure storage implementation for Google OAuth credentials.
/// Uses Windows DPAPI (ProtectedData, CurrentUser scope) so tokens are stored encrypted at rest
/// on the local machine and never leak into databases, logs, or backup packages.
/// </summary>
public class DpapiDriveTokenStore : IDriveTokenStore
{
    private readonly IAppPaths _appPaths;
    private readonly ILogger<DpapiDriveTokenStore> _logger;

    public DpapiDriveTokenStore(
        IAppPaths appPaths,
        ILogger<DpapiDriveTokenStore>? logger = null)
    {
        _appPaths = appPaths;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DpapiDriveTokenStore>.Instance;
    }

    public async Task SaveTokenAsync(string tokenJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenJson))
        {
            await DeleteTokenAsync(cancellationToken);
            return;
        }

        try
        {
            _appPaths.EnsureDirectoriesExist();
            byte[] plainBytes = Encoding.UTF8.GetBytes(tokenJson);
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

            string tokenPath = _appPaths.GoogleTokenFilePath;
            string tempPath = tokenPath + ".tmp";

            await File.WriteAllBytesAsync(tempPath, cipherBytes, cancellationToken);
            if (File.Exists(tokenPath))
            {
                File.Delete(tokenPath);
            }
            File.Move(tempPath, tokenPath);

            _logger.LogInformation("Saved encrypted Google Drive token to '{Path}'", tokenPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to protect and save Google Drive credentials");
            throw;
        }
    }

    public async Task<string?> LoadTokenAsync(CancellationToken cancellationToken = default)
    {
        string tokenPath = _appPaths.GoogleTokenFilePath;
        if (!File.Exists(tokenPath))
        {
            return null;
        }

        try
        {
            byte[] cipherBytes = await File.ReadAllBytesAsync(tokenPath, cancellationToken);
            if (cipherBytes.Length == 0)
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

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load/decrypt Google Drive credentials from '{Path}'", tokenPath);
            return null;
        }
    }

    public Task DeleteTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string tokenPath = _appPaths.GoogleTokenFilePath;
            if (File.Exists(tokenPath))
            {
                File.Delete(tokenPath);
                _logger.LogInformation("Deleted Google Drive credentials from '{Path}'", tokenPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Google Drive credentials file");
        }

        return Task.CompletedTask;
    }

    public bool HasToken()
    {
        string tokenPath = _appPaths.GoogleTokenFilePath;
        return File.Exists(tokenPath) && new FileInfo(tokenPath).Length > 0;
    }
}

