using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ShopPOS.Tests;

public class HexColorScanningTests
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
    public void NoHardcodedHexColorsInViewsOrDialogsOutsideThemeDictionaries()
    {
        string uiViewsDir = Path.Combine(SolutionRoot, "ShopPOS.UI", "Views");
        var xamlFiles = Directory.GetFiles(uiViewsDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
            .ToList();

        // Also check MainWindow.xaml if not already in Views
        string mainWindowXaml = Path.Combine(SolutionRoot, "ShopPOS.UI", "MainWindow.xaml");
        if (File.Exists(mainWindowXaml) && !xamlFiles.Contains(mainWindowXaml))
        {
            xamlFiles.Add(mainWindowXaml);
        }

        // Regex matching hex colors like #FFF, #FFFFFF, #FFFFFFFF
        var hexRegex = new Regex(@"#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b");

        var violations = new List<string>();

        foreach (var file in xamlFiles)
        {
            string fileName = Path.GetFileName(file);
            // Theme files are allowed to declare the palette colors
            if (fileName.Equals("ThemeLight.xaml", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("ThemeDark.xaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var match = hexRegex.Match(lines[i]);
                if (match.Success)
                {
                    violations.Add($"{Path.GetRelativePath(SolutionRoot, file)}:{i + 1} - Found hex color '{match.Value}': {lines[i].Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Found {violations.Count} hardcoded hex colors in views/dialogs. All colors must come from ThemeLight/ThemeDark resources:\n" +
            string.Join("\n", violations));
    }
}

