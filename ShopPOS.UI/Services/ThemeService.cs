using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace ShopPOS.UI.Services;

/// <summary>
/// Implements application theme switching and persistence.
/// </summary>
public class ThemeService : IThemeService
{
    private string _currentTheme = "Light";
    private readonly string _settingsFilePath;

    public string CurrentTheme => _currentTheme;

    public event Action<string>? ThemeChanged;

    public ThemeService()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string configDir = Path.Combine(localAppData, "ShopPOS");
        _settingsFilePath = Path.Combine(configDir, "theme.txt");

        LoadPersistedTheme();
    }

    public void SetTheme(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme)) return;
        theme = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";

        _currentTheme = theme;
        ApplyThemeToResources(theme);
        SavePersistedTheme(theme);
        ThemeChanged?.Invoke(_currentTheme);
    }

    public void ToggleTheme()
    {
        string newTheme = _currentTheme.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
        SetTheme(newTheme);
    }

    private void ApplyThemeToResources(string theme)
    {
        if (Application.Current == null) return;

        var merged = Application.Current.Resources.MergedDictionaries;
        var existingTheme = merged.FirstOrDefault(d =>
            d.Source != null && (d.Source.OriginalString.Contains("ThemeLight.xaml", StringComparison.OrdinalIgnoreCase) ||
                                 d.Source.OriginalString.Contains("ThemeDark.xaml", StringComparison.OrdinalIgnoreCase)));

        var newThemeDict = new ResourceDictionary
        {
            Source = new Uri($"/ShopPOS.UI;component/Styles/Theme{theme}.xaml", UriKind.RelativeOrAbsolute)
        };

        if (existingTheme != null)
        {
            int index = merged.IndexOf(existingTheme);
            merged.RemoveAt(index);
            merged.Insert(index, newThemeDict);
        }
        else
        {
            merged.Insert(0, newThemeDict);
        }
    }

    private void LoadPersistedTheme()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string saved = File.ReadAllText(_settingsFilePath).Trim();
                if (saved.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                {
                    _currentTheme = "Dark";
                }
            }
        }
        catch
        {
            _currentTheme = "Light";
        }
    }

    private void SavePersistedTheme(string theme)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_settingsFilePath, theme);
        }
        catch
        {
            // Ignore persistence failures in read-only / test environments
        }
    }
}

