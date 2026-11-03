using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ShopPOS.Tests;

public class CurrencyFormatScanningTests
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
    public void NoXamlOrCsFilesUseStandardCurrencyFormatStringC()
    {
        var xamlFiles = Directory.GetFiles(SolutionRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\"));

        var csFiles = Directory.GetFiles(SolutionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\") && !f.EndsWith("CurrencyFormatScanningTests.cs"));

        var violations = new System.Collections.Generic.List<string>();

        // Regex for :C or {0:C or StringFormat=C
        var regexC = new Regex(@"StringFormat=[^{]*\{0:C|\{0:C[0-9]*\}|:C[0-9]*\}|StringFormat=C[0-9]*", RegexOptions.IgnoreCase);

        foreach (var file in xamlFiles.Concat(csFiles))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (regexC.IsMatch(lines[i]))
                {
                    violations.Add($"{file}:{i + 1} - {lines[i].Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Found {violations.Count} files using standard :C / StringFormat=C currency formatting:\n" +
            string.Join("\n", violations.Take(10)));
    }

    [Fact]
    public void NoXamlOrCsFilesUseDollarSignAsCurrencyPrefix()
    {
        var xamlFiles = Directory.GetFiles(SolutionRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\"));

        var csFiles = Directory.GetFiles(SolutionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\") && !f.EndsWith("CurrencyFormatScanningTests.cs"));

        var violations = new System.Collections.Generic.List<string>();

        // Check for literal $ used in currency contexts: e.g. $"${amount}" or Text="${Binding..." or StringFormat='${0}'
        var regexDollar = new Regex(@"""\$\{|\$\""\$\{|\""\$[0-9]|StringFormat='?\$", RegexOptions.IgnoreCase);

        foreach (var file in xamlFiles.Concat(csFiles))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (regexDollar.IsMatch(lines[i]))
                {
                    violations.Add($"{file}:{i + 1} - {lines[i].Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Found {violations.Count} files with literal dollar currency symbol usage:\n" +
            string.Join("\n", violations.Take(10)));
    }
}

