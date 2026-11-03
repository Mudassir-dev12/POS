using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;

namespace ShopPOS.Tests;

public class ThemeContrastTests
{
    private static readonly string SolutionRoot = FindSolutionRoot();

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ShopPOS.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? AppContext.BaseDirectory;
    }

    [Fact]
    public void ThemeLightAndThemeDark_HaveMatchingKeysWithoutMissingEntries()
    {
        string lightPath = Path.Combine(SolutionRoot, "ShopPOS.UI", "Styles", "ThemeLight.xaml");
        string darkPath = Path.Combine(SolutionRoot, "ShopPOS.UI", "Styles", "ThemeDark.xaml");

        Assert.True(File.Exists(lightPath), $"ThemeLight.xaml not found at {lightPath}");
        Assert.True(File.Exists(darkPath), $"ThemeDark.xaml not found at {darkPath}");

        var lightDict = LoadDictionary(lightPath);
        var darkDict = LoadDictionary(darkPath);

        var lightKeys = lightDict.Keys.Cast<object>().Select(k => k.ToString()!).ToHashSet();
        var darkKeys = darkDict.Keys.Cast<object>().Select(k => k.ToString()!).ToHashSet();

        var missingInDark = lightKeys.Except(darkKeys).ToList();
        var missingInLight = darkKeys.Except(lightKeys).ToList();

        Assert.True(missingInDark.Count == 0, $"Keys in Light but missing in Dark: {string.Join(", ", missingInDark)}");
        Assert.True(missingInLight.Count == 0, $"Keys in Dark but missing in Light: {string.Join(", ", missingInLight)}");
    }

    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("ThemeDark.xaml")]
    public void AllInteractiveStates_HaveAccessibleContrastRatio_AndForegroundDoesNotEqualBackground(string themeFileName)
    {
        string themePath = Path.Combine(SolutionRoot, "ShopPOS.UI", "Styles", themeFileName);
        var dict = LoadDictionary(themePath);

        // Pairs of (BackgroundKey, ForegroundKey, MinContrastRatio, Description)
        var pairsToCheck = new List<(string Bg, string Fg, double MinRatio, string Desc)>
        {
            // Primary Button
            ("PrimaryColor", "PrimaryTextColor", 4.5, "Primary Normal"),
            ("PrimaryHoverColor", "PrimaryTextColor", 4.5, "Primary Hover"),
            ("PrimaryPressedColor", "PrimaryTextColor", 4.5, "Primary Pressed"),
            ("DisabledBackgroundColor", "DisabledTextColor", 4.0, "Disabled Button"),

            // Secondary / Outlined Button
            ("SecondaryBackgroundColor", "SecondaryTextColor", 4.5, "Secondary Normal"),
            ("SecondaryHoverBackgroundColor", "SecondaryHoverTextColor", 4.5, "Secondary Hover"),
            ("SecondaryPressedBackgroundColor", "SecondaryPressedTextColor", 4.5, "Secondary Pressed"),

            // Danger Button
            ("DangerColor", "DangerTextColor", 4.5, "Danger Normal"),
            ("DangerHoverColor", "DangerTextColor", 4.5, "Danger Hover"),
            ("DangerPressedColor", "DangerTextColor", 4.5, "Danger Pressed"),

            // Sidebar / Nav Items (Icons/Labels >= 3.0:1)
            ("SidebarBackgroundColor", "SidebarTextColor", 3.0, "Sidebar Normal Item"),
            ("SidebarHoverColor", "SidebarTextActiveColor", 4.5, "Sidebar Hover Item"),
            ("SidebarActiveColor", "SidebarTextActiveColor", 4.5, "Sidebar Active Item"),

            // DataGrid Rows & Headers
            ("CardBackgroundColor", "TextPrimaryColor", 4.5, "DataGrid Normal Row"),
            ("CardBackgroundTranslucentColor", "TextPrimaryColor", 4.5, "DataGrid Alternating Row"),
            ("TableRowHoverBackgroundColor", "TableRowHoverTextColor", 4.5, "DataGrid Hover Row"),
            ("TableRowSelectedBackgroundColor", "TableRowSelectedTextColor", 4.5, "DataGrid Selected Row"),
            ("TableHeaderBackgroundColor", "TableHeaderTextColor", 4.5, "DataGrid Column Header"),

            // Inputs
            ("InputBackgroundColor", "InputTextColor", 4.5, "Input Field Normal"),

            // Tooltips
            ("ToolTipBackgroundColor", "ToolTipTextColor", 4.5, "ToolTip Normal")
        };

        var failures = new List<string>();

        foreach (var (bgKey, fgKey, minRatio, desc) in pairsToCheck)
        {
            Color bgColor = GetColor(dict, bgKey);
            Color fgColor = GetColor(dict, fgKey);

            if (bgColor == fgColor)
            {
                failures.Add($"[{themeFileName}] {desc}: Foreground equals Background ({fgColor})");
                continue;
            }

            double ratio = CalculateContrastRatio(bgColor, fgColor);
            if (ratio < minRatio)
            {
                failures.Add($"[{themeFileName}] {desc}: Contrast ratio {ratio:F2}:1 is below required {minRatio:F2}:1 (Bg={bgColor}, Fg={fgColor})");
            }
        }

        Assert.True(failures.Count == 0,
            $"Contrast violations found in {themeFileName}:\n" + string.Join("\n", failures));
    }

    private static System.Windows.ResourceDictionary LoadDictionary(string path)
    {
        using var stream = File.OpenRead(path);
        return (System.Windows.ResourceDictionary)XamlReader.Load(stream);
    }

    private static Color GetColor(System.Windows.ResourceDictionary dict, string key)
    {
        if (dict.Contains(key))
        {
            var val = dict[key];
            if (val is Color c) return c;
            if (val is SolidColorBrush b) return b.Color;
        }

        throw new KeyNotFoundException($"Key '{key}' not found or not a Color/SolidColorBrush in ResourceDictionary.");
    }

    private static double CalculateContrastRatio(Color c1, Color c2)
    {
        double l1 = GetRelativeLuminance(c1);
        double l2 = GetRelativeLuminance(c2);

        double brighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);

        return (brighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(Color color)
    {
        double r = TransformComponent(color.R / 255.0);
        double g = TransformComponent(color.G / 255.0);
        double b = TransformComponent(color.B / 255.0);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double TransformComponent(double val)
    {
        return val <= 0.03928
            ? val / 12.92
            : Math.Pow((val + 0.055) / 1.055, 2.4);
    }
}

