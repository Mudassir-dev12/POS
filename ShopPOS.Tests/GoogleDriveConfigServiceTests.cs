using System;
using System.IO;
using AwesomeAssertions;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class GoogleDriveConfigServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly IAppPaths _appPaths;

    public GoogleDriveConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"ShopPOS_GConfigTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _appPaths = new AppPaths(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
        Environment.SetEnvironmentVariable("SHOPPOS_GDRIVE_CLIENT_ID", null);
        Environment.SetEnvironmentVariable("SHOPPOS_GDRIVE_CLIENT_SECRET", null);
    }

    [Fact]
    public void GetConfig_ParsesFlatJsonFile()
    {
        string json = "{\"client_id\": \"flat-id-123\", \"client_secret\": \"flat-sec-456\"}";
        File.WriteAllText(_appPaths.GoogleClientConfigPath, json);

        var service = new GoogleDriveConfigService(_appPaths);
        var config = service.GetConfig();

        config.IsConfigured.Should().BeTrue();
        config.ClientId.Should().Be("flat-id-123");
        config.ClientSecret.Should().Be("flat-sec-456");
    }

    [Fact]
    public void GetConfig_ParsesNestedInstalledJsonFile()
    {
        string json = "{\"installed\": {\"client_id\": \"nested-id-789\", \"client_secret\": \"nested-sec-012\"}}";
        File.WriteAllText(_appPaths.GoogleClientConfigPath, json);

        var service = new GoogleDriveConfigService(_appPaths);
        var config = service.GetConfig();

        config.IsConfigured.Should().BeTrue();
        config.ClientId.Should().Be("nested-id-789");
        config.ClientSecret.Should().Be("nested-sec-012");
    }

    [Fact]
    public void GetConfig_ParsesNestedWebJsonFile()
    {
        string json = "{\"web\": {\"client_id\": \"web-id-345\", \"client_secret\": \"web-sec-678\"}}";
        File.WriteAllText(_appPaths.GoogleClientConfigPath, json);

        var service = new GoogleDriveConfigService(_appPaths);
        var config = service.GetConfig();

        config.IsConfigured.Should().BeTrue();
        config.ClientId.Should().Be("web-id-345");
        config.ClientSecret.Should().Be("web-sec-678");
    }

    [Fact]
    public void GetConfig_FallsBackToEnvironmentVariables()
    {
        // No file on disk
        if (File.Exists(_appPaths.GoogleClientConfigPath)) File.Delete(_appPaths.GoogleClientConfigPath);

        Environment.SetEnvironmentVariable("SHOPPOS_GDRIVE_CLIENT_ID", "env-client-id");
        Environment.SetEnvironmentVariable("SHOPPOS_GDRIVE_CLIENT_SECRET", "env-client-secret");

        var service = new GoogleDriveConfigService(_appPaths);
        var config = service.GetConfig();

        config.IsConfigured.Should().BeTrue();
        config.ClientId.Should().Be("env-client-id");
        config.ClientSecret.Should().Be("env-client-secret");
    }

    [Fact]
    public void GetConfig_ReturnsUnconfigured_WhenNoFileAndNoEnvVars()
    {
        if (File.Exists(_appPaths.GoogleClientConfigPath)) File.Delete(_appPaths.GoogleClientConfigPath);
        Environment.SetEnvironmentVariable("SHOPPOS_GDRIVE_CLIENT_ID", null);
        Environment.SetEnvironmentVariable("SHOPPOS_GDRIVE_CLIENT_SECRET", null);

        var service = new GoogleDriveConfigService(_appPaths);
        var config = service.GetConfig();

        config.IsConfigured.Should().BeFalse();
        service.IsConfigured().Should().BeFalse();
    }
}

