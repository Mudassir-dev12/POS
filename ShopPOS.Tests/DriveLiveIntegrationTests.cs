using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

/// <summary>
/// Live integration test against real Google Drive endpoints.
/// Strictly skipped in automated test suites unless SHOPPOS_GDRIVE_LIVE_TEST is explicitly enabled.
/// </summary>
public class DriveLiveIntegrationTests
{
    [Fact]
    [Trait("Category", "Slow")]
    public async Task CheckConnectivityAsync_RealGoogleServer_ReturnsTrueWhenConnected()
    {
        string? runLive = Environment.GetEnvironmentVariable("SHOPPOS_GDRIVE_LIVE_TEST");
        if (!string.Equals(runLive, "1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(runLive, "true", StringComparison.OrdinalIgnoreCase))
        {
            // Skip when live tests are not explicitly opted in
            return;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"ShopPOS_LiveDrive_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var appPaths = new AppPaths(tempDir);
            var configService = new GoogleDriveConfigService(appPaths);
            var tokenStore = new DpapiDriveTokenStore(appPaths);
            var client = new GoogleDriveClient(configService, tokenStore);

            bool reachable = await client.CheckConnectivityAsync();
            reachable.Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}

