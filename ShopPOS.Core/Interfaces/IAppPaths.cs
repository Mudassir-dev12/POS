namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Central contract for resolving application directory and file paths.
/// Ensures all file locations are uniformly managed and easily redirected in test environments.
/// </summary>
public interface IAppPaths
{
    /// <summary>Root application data directory (default: C:\ShopData).</summary>
    string DataDirectory { get; }

    /// <summary>Absolute path to the primary SQLite database file (shop.db).</summary>
    string DatabasePath { get; }

    /// <summary>Directory for rolling application log files.</summary>
    string LogsDirectory { get; }

    /// <summary>Directory for database backup archives.</summary>
    string BackupsDirectory { get; }

    /// <summary>Directory for catalog product images.</summary>
    string ImagesDirectory { get; }

    /// <summary>Directory for temporary working files.</summary>
    string TempDirectory { get; }

    /// <summary>Path to the lock file used for unclean shutdown detection.</summary>
    string LockFilePath { get; }

    /// <summary>Path to the DPAPI-encrypted Google Drive token file.</summary>
    string GoogleTokenFilePath { get; }

    /// <summary>Path to the DPAPI-encrypted GitHub Personal Access Token file.</summary>
    string GitHubTokenFilePath { get; }

    /// <summary>Path to the optional googledrive.client.json configuration file next to the app.</summary>
    string GoogleClientConfigPath { get; }

    /// <summary>
    /// Creates all standard directories if they do not already exist.
    /// </summary>
    void EnsureDirectoriesExist();
}

