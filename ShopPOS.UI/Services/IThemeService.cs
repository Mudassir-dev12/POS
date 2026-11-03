using System;

namespace ShopPOS.UI.Services;

/// <summary>
/// Service for managing application themes (Light and Dark) and runtime toggling.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the current active theme name ("Light" or "Dark").
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Event raised when the theme has changed.
    /// </summary>
    event Action<string>? ThemeChanged;

    /// <summary>
    /// Sets the application theme explicitly.
    /// </summary>
    void SetTheme(string theme);

    /// <summary>
    /// Toggles between Light and Dark themes.
    /// </summary>
    void ToggleTheme();
}

