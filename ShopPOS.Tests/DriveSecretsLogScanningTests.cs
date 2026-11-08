using System;
using System.IO;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace ShopPOS.Tests;

public class DriveSecretsLogScanningTests
{
    [Fact]
    public void SourceCode_MustNotContainHardCodedOAuthSecretsOrTokens()
    {
        string rootDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        var filesToScan = Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj") && !f.Contains("bin") && !f.Contains("Migrations"))
            .ToList();

        // Check for real Google client secret formats (e.g. GOCSPX-...)
        var secretPattern = new Regex(@"GOCSPX-[a-zA-Z0-9_-]{28,}", RegexOptions.Compiled);
        var tokenPattern = new Regex(@"ya29\.[a-zA-Z0-9_-]{50,}", RegexOptions.Compiled);

        foreach (var file in filesToScan)
        {
            string content = File.ReadAllText(file);
            secretPattern.IsMatch(content).Should().BeFalse($"Found hard-coded Google client secret in {file}");
            tokenPattern.IsMatch(content).Should().BeFalse($"Found hard-coded Google access token in {file}");
        }
    }

    [Fact]
    public void GitIgnore_MustIncludeGoogleDriveClientSecretsAndTokens()
    {
        string rootDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        string gitIgnorePath = Path.Combine(rootDir, ".gitignore");

        File.Exists(gitIgnorePath).Should().BeTrue();
        string gitIgnore = File.ReadAllText(gitIgnorePath);

        gitIgnore.Should().Contain("googledrive.client.json");
        gitIgnore.Should().Contain("google_token.dat");
        gitIgnore.Should().Contain("github_token.dat");
    }

    [Fact]
    public async Task DpapiGitHubTokenStore_MustRoundTripSecurely()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ShopPOS_TokenTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var appPaths = new ShopPOS.Core.Services.AppPaths(tempDir);
            var tokenStore = new ShopPOS.Data.Services.DpapiGitHubTokenStore(appPaths);

            (await tokenStore.HasTokenAsync()).Should().BeFalse();

            string testToken = "ghp_MockToken1234567890abcdefghijklmnopqrstuvwxyz";
            await tokenStore.SaveTokenAsync(testToken);

            (await tokenStore.HasTokenAsync()).Should().BeTrue();
            string? readBack = await tokenStore.GetTokenAsync();
            readBack.Should().Be(testToken);

            await tokenStore.DeleteTokenAsync();
            (await tokenStore.HasTokenAsync()).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GoogleDriveClientSampleJson_MustExistWithPlaceholders()
    {
        string rootDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        string samplePath = Path.Combine(rootDir, "googledrive.client.sample.json");

        File.Exists(samplePath).Should().BeTrue();
        string sample = File.ReadAllText(samplePath);

        sample.Should().Contain("client_id");
        sample.Should().Contain("client_secret");
        sample.Should().Contain("YOUR_GOOGLE_CLIENT_ID_HERE");
    }
}

