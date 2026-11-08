using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Loads Google OAuth Client ID and Secret from googledrive.client.json or environment variables.
/// Never logs secret keys and never commits secrets.
/// </summary>
public class GoogleDriveConfigService : IGoogleDriveConfigService
{
    private readonly IAppPaths _appPaths;
    private readonly ILogger<GoogleDriveConfigService> _logger;

    public GoogleDriveConfigService(
        IAppPaths appPaths,
        ILogger<GoogleDriveConfigService>? logger = null)
    {
        _appPaths = appPaths;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleDriveConfigService>.Instance;
    }

    public GoogleDriveClientConfig GetConfig()
    {
        var config = new GoogleDriveClientConfig();

        // 1. Try reading from googledrive.client.json next to application / data path / current directory
        var candidatePaths = new[]
        {
            _appPaths.GoogleClientConfigPath,
            Path.Combine(_appPaths.DataDirectory, "googledrive.client.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "googledrive.client.json")
        };

        string? configPath = candidatePaths.FirstOrDefault(File.Exists);
        if (configPath != null)
        {
            try
            {
                string json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check for flat structure { "client_id": "...", "client_secret": "..." }
                if (root.TryGetProperty("client_id", out var clientIdElem) &&
                    root.TryGetProperty("client_secret", out var clientSecretElem))
                {
                    config.ClientId = clientIdElem.GetString() ?? string.Empty;
                    config.ClientSecret = clientSecretElem.GetString() ?? string.Empty;
                }
                // Check for Google Cloud Console downloaded format { "installed": { "client_id": "...", "client_secret": "..." } }
                else if (root.TryGetProperty("installed", out var installedElem))
                {
                    if (installedElem.TryGetProperty("client_id", out var nestedId))
                        config.ClientId = nestedId.GetString() ?? string.Empty;
                    if (installedElem.TryGetProperty("client_secret", out var nestedSecret))
                        config.ClientSecret = nestedSecret.GetString() ?? string.Empty;
                }
                // Check for web client format { "web": { "client_id": "...", "client_secret": "..." } }
                else if (root.TryGetProperty("web", out var webElem))
                {
                    if (webElem.TryGetProperty("client_id", out var webId))
                        config.ClientId = webId.GetString() ?? string.Empty;
                    if (webElem.TryGetProperty("client_secret", out var webSecret))
                        config.ClientSecret = webSecret.GetString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Google Drive client configuration file at '{Path}'", configPath);
            }
        }

        // 2. Fall back to environment variables if still unpopulated
        if (string.IsNullOrWhiteSpace(config.ClientId))
        {
            config.ClientId = Environment.GetEnvironmentVariable("SHOPPOS_GDRIVE_CLIENT_ID") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            config.ClientSecret = Environment.GetEnvironmentVariable("SHOPPOS_GDRIVE_CLIENT_SECRET") ?? string.Empty;
        }

        return config;
    }

    public bool IsConfigured()
    {
        var config = GetConfig();
        return config.IsConfigured;
    }
}

