using System.IO;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Core.Services;

/// <summary>
/// Default implementation of IAppPaths resolving directories relative to a configured root.
/// </summary>
public class AppPaths : IAppPaths
{
    public const string DefaultRootPath = @"C:\ShopData";

    public string DataDirectory { get; }
    public string DatabasePath => Path.Combine(DataDirectory, "shop.db");
    public string LogsDirectory => Path.Combine(DataDirectory, "Logs");
    public string BackupsDirectory => Path.Combine(DataDirectory, "Backups");
    public string ImagesDirectory => Path.Combine(DataDirectory, "Images");
    public string TempDirectory => Path.Combine(DataDirectory, "Temp");
    public string LockFilePath => Path.Combine(DataDirectory, ".lock");
    public string GoogleTokenFilePath => Path.Combine(DataDirectory, "google_token.dat");
    public string GitHubTokenFilePath => Path.Combine(DataDirectory, "github_token.dat");
    public string GoogleClientConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "googledrive.client.json");

    public AppPaths(string? dataDirectory = null)
    {
        DataDirectory = !string.IsNullOrWhiteSpace(dataDirectory)
            ? Path.GetFullPath(dataDirectory)
            : DefaultRootPath;
    }

    public void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(DataDirectory)) Directory.CreateDirectory(DataDirectory);
        if (!Directory.Exists(LogsDirectory)) Directory.CreateDirectory(LogsDirectory);
        if (!Directory.Exists(BackupsDirectory)) Directory.CreateDirectory(BackupsDirectory);
        if (!Directory.Exists(ImagesDirectory)) Directory.CreateDirectory(ImagesDirectory);
        if (!Directory.Exists(TempDirectory)) Directory.CreateDirectory(TempDirectory);
    }
}

