namespace ShopPOS.Core.Models;

/// <summary>
/// User and machine-specific configuration stored in appsettings.user.json.
/// Precedes database initialization so the app can locate the data directory.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Custom root data directory path (default: C:\ShopData).
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>
    /// Optional URL checked for available software updates.
    /// When null or empty, the update button is hidden.
    /// </summary>
    public string? UpdateInfoUrl { get; set; }

    /// <summary>
    /// Saved window layout and geometry.
    /// </summary>
    public WindowSettings Window { get; set; } = new();
}

/// <summary>
/// Persisted window bounds and sidebar display state across sessions.
/// </summary>
public class WindowSettings
{
    public double Width { get; set; } = 1366;
    public double Height { get; set; } = 768;
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public int WindowState { get; set; } // 0 = Normal, 2 = Maximized
    public bool IsSidebarCollapsed { get; set; }
}

