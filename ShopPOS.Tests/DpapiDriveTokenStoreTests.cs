using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class DpapiDriveTokenStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly IAppPaths _appPaths;

    public DpapiDriveTokenStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"ShopPOS_TokenTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _appPaths = new AppPaths(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task SaveAndLoadToken_RoundTripMatches()
    {
        var store = new DpapiDriveTokenStore(_appPaths);
        store.HasToken().Should().BeFalse();

        string sampleToken = "{\"access_token\":\"ya29.sample\",\"refresh_token\":\"1//sample_refresh\"}";
        await store.SaveTokenAsync(sampleToken);

        store.HasToken().Should().BeTrue();
        File.Exists(_appPaths.GoogleTokenFilePath).Should().BeTrue();

        string? loaded = await store.LoadTokenAsync();
        loaded.Should().Be(sampleToken);
    }

    [Fact]
    public async Task DeleteToken_RemovesFileAndReturnsNull()
    {
        var store = new DpapiDriveTokenStore(_appPaths);
        await store.SaveTokenAsync("{\"token\":\"test\"}");
        store.HasToken().Should().BeTrue();

        await store.DeleteTokenAsync();
        store.HasToken().Should().BeFalse();

        string? loaded = await store.LoadTokenAsync();
        loaded.Should().BeNull();
    }
}

