using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ShopPOS.Tests;

public class EmojiScanningTests
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
    public void NoEmojisOrPictographsInXamlCsOrMdFiles()
    {
        var extensions = new[] { "*.xaml", "*.cs", "*.md" };
        var files = extensions.SelectMany(ext => Directory.GetFiles(SolutionRoot, ext, SearchOption.AllDirectories))
            .Where(f => !f.Contains("\\bin\\") &&
                        !f.Contains("\\obj\\") &&
                        !f.Contains("\\.git\\") &&
                        !f.Contains("\\.gemini\\") &&
                        !f.EndsWith("EmojiScanningTests.cs"));

        var violations = new List<string>();

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            for (int i = 0; i < content.Length; i++)
            {
                int codePoint = char.ConvertToUtf32(content, i);
                if (char.IsSurrogatePair(content, i))
                {
                    i++; // skip next surrogate char
                }

                if (IsEmojiOrPictograph(codePoint))
                {
                    violations.Add($"{Path.GetFileName(file)} at index {i}: U+{codePoint:X4} (\\u{codePoint:X4})");
                    break; // report once per file
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Found {violations.Count} files containing emojis/pictographs:\n" +
            string.Join("\n", violations));
    }

    private static bool IsEmojiOrPictograph(int cp)
    {
        // Emoji & Symbols ranges:
        // U+1F300..U+1FAFF: Miscellaneous Symbols and Pictographs, Emoticons, Transport, etc.
        // U+2600..U+27BF: Misc Symbols & Dingbats
        // U+2B00..U+2BFF: Misc Symbols and Arrows
        // U+FE0F: Variation Selector-16
        // U+200D: Zero Width Joiner
        return (cp >= 0x1F300 && cp <= 0x1FAFF) ||
               (cp >= 0x2600 && cp <= 0x27BF) ||
               (cp >= 0x2B00 && cp <= 0x2BFF) ||
               cp == 0xFE0F ||
               cp == 0x200D;
    }
}

