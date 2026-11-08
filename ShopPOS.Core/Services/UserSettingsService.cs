using System;
using System.IO;
using System.Text.Json;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

/// <summary>
/// Implements local user configuration storage and filesystem capability checks.
/// </summary>
public class UserSettingsService : IUserSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string? _overrideConfigPath;

    public UserSettingsService(string? overrideConfigPath = null)
    {
        _overrideConfigPath = overrideConfigPath;
    }

    public string GetConfigFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_overrideConfigPath))
        {
            return _overrideConfigPath;
        }

        // Try next to executable first
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string exeConfigPath = Path.Combine(exeDir, "appsettings.user.json");

        // If file already exists next to exe, use it
        if (File.Exists(exeConfigPath))
        {
            return exeConfigPath;
        }

        // Check if exe directory is writable (e.g. portable mode vs Program Files)
        if (IsDirectoryWritable(exeDir, out _))
        {
            return exeConfigPath;
        }

        // Otherwise use %LocalAppData%\ShopPOS\appsettings.user.json
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDir = Path.Combine(localAppData, "ShopPOS");
        return Path.Combine(appDir, "appsettings.user.json");
    }

    public UserSettings Load()
    {
        string path = GetConfigFilePath();
        if (!File.Exists(path))
        {
            return new UserSettings();
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        string path = GetConfigFilePath();
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }

    public string ResolveDataDirectory(string? commandLineDir = null)
    {
        // 1. Command-line argument override takes highest precedence
        if (!string.IsNullOrWhiteSpace(commandLineDir))
        {
            return Path.GetFullPath(commandLineDir);
        }

        // 2. User settings file
        var settings = Load();
        if (!string.IsNullOrWhiteSpace(settings.DataDirectory))
        {
            string candidate = Path.GetFullPath(settings.DataDirectory);
            if (IsDirectoryWritable(candidate, out _))
            {
                return candidate;
            }
        }

        // 3. Test default C:\ShopData
        if (IsDirectoryWritable(AppPaths.DefaultRootPath, out _))
        {
            return AppPaths.DefaultRootPath;
        }

        // 4. Automatic non-admin fallback: %LocalAppData%\ShopPOS\Data
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string fallback = Path.Combine(localAppData, "ShopPOS", "Data");
        return fallback;
    }

    public void SaveDataDirectory(string directoryPath)
    {
        var settings = Load();
        settings.DataDirectory = Path.GetFullPath(directoryPath);
        Save(settings);
    }

    public bool IsDirectoryWritable(string directoryPath, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string testFile = Path.Combine(directoryPath, $".perm_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public long GetAvailableFreeSpaceBytes(string directoryPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(directoryPath);
            string? root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return 0;
            }

            var drive = new DriveInfo(root);
            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    public bool HasSufficientSpace(string directoryPath, long requiredBytes = 500 * 1024 * 1024)
    {
        long freeSpace = GetAvailableFreeSpaceBytes(directoryPath);
        return freeSpace >= requiredBytes;
    }
}

