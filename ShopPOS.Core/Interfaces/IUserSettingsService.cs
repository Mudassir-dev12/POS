using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Service for managing local user preferences and data directory redirection (appsettings.user.json).
/// </summary>
public interface IUserSettingsService
{
    /// <summary>
    /// Loads the persisted user settings from disk or returns defaults.
    /// </summary>
    UserSettings Load();

    /// <summary>
    /// Saves user settings to appsettings.user.json.
    /// </summary>
    void Save(UserSettings settings);

    /// <summary>
    /// Gets the resolved data directory path, checking environment, user settings, or default.
    /// </summary>
    string ResolveDataDirectory(string? commandLineDir = null);

    /// <summary>
    /// Saves the custom data directory path.
    /// </summary>
    void SaveDataDirectory(string directoryPath);

    /// <summary>
    /// Checks whether the target directory can be created/written to by creating a test file.
    /// </summary>
    bool IsDirectoryWritable(string directoryPath, out string? errorMessage);

    /// <summary>
    /// Returns the available free disk space in bytes for the specified directory's drive.
    /// </summary>
    long GetAvailableFreeSpaceBytes(string directoryPath);

    /// <summary>
    /// Checks if the drive containing the directory has at least the required free space.
    /// </summary>
    bool HasSufficientSpace(string directoryPath, long requiredBytes = 500 * 1024 * 1024);
}

