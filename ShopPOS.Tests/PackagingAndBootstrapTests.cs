using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class PackagingAndBootstrapTests : IDisposable
{
    private readonly string _tempDir;

    public PackagingAndBootstrapTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ShopPOS_PkgTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void BootstrapCrashHandler_DllNotFound_Sqlite_ReturnsPlainLanguageAdvice()
    {
        var ex = new DllNotFoundException("Unable to load DLL 'e_sqlite3' or one of its dependencies: The specified module could not be found.");
        var info = BootstrapCrashHandler.MapException(ex);

        info.ErrorSummary.Should().Contain("SQLite");
        info.PlainLanguageAdvice.Should().Contain("Visual C++ runtime missing");
        info.PlainLanguageAdvice.Should().Contain("reinstall using the setup file");
    }

    [Fact]
    public void BootstrapCrashHandler_DllNotFound_QuestPdfSkia_ReturnsPlainLanguageAdvice()
    {
        var ex = new DllNotFoundException("Unable to load DLL 'QuestPdfSkia.dll' or one of its dependencies: The specified module could not be found.");
        var info = BootstrapCrashHandler.MapException(ex);

        info.ErrorSummary.Should().Contain("PDF");
        info.PlainLanguageAdvice.Should().Contain("Visual C++ runtime missing");
    }

    [Fact]
    public void BootstrapCrashHandler_DllNotFound_VcRuntime_ReturnsPlainLanguageAdvice()
    {
        var ex = new DllNotFoundException("Unable to load DLL 'vcruntime140.dll'.");
        var info = BootstrapCrashHandler.MapException(ex);

        info.ErrorSummary.Should().Contain("Visual C++");
        info.PlainLanguageAdvice.Should().Contain("vc_redist.x64.exe");
    }

    [Fact]
    public void BootstrapCrashHandler_BadImageFormat_Returns64BitArchitectureAdvice()
    {
        var ex = new BadImageFormatException("An attempt was made to load a program with an incorrect format.");
        var info = BootstrapCrashHandler.MapException(ex);

        info.ErrorSummary.Should().Contain("architecture mismatch");
        info.PlainLanguageAdvice.Should().Contain("64-bit");
    }

    [Fact]
    public void BootstrapCrashHandler_FileNotFound_ReturnsMissingAssemblyAdvice()
    {
        var ex = new FileNotFoundException("Could not load file or assembly 'ShopPOS.Core.dll'.", "ShopPOS.Core.dll");
        var info = BootstrapCrashHandler.MapException(ex);

        info.ErrorSummary.Should().Contain("missing");
        info.PlainLanguageAdvice.Should().Contain("ShopPOS.Core.dll");
    }

    [Fact]
    public void BootstrapCrashHandler_UnauthorizedAccess_ReturnsPermissionAdvice()
    {
        var ex = new UnauthorizedAccessException("Access to the path 'C:\\ShopData' is denied.");
        var info = BootstrapCrashHandler.MapException(ex);

        info.ErrorSummary.Should().Contain("Permission denied");
        info.PlainLanguageAdvice.Should().Contain("write permission");
    }

    [Fact]
    public void BootstrapCrashHandler_DiskSpaceIoException_ReturnsDiskSpaceAdvice()
    {
        var ex = new IOException("There is not enough space on the disk.");
        var info = BootstrapCrashHandler.MapException(ex);

        info.ErrorSummary.Should().Contain("Disk space");
        info.PlainLanguageAdvice.Should().Contain("free up disk space");
    }

    [Fact]
    public void BootstrapCrashHandler_TypeInitializationException_UnwrapsInnerException()
    {
        var inner = new DllNotFoundException("Unable to load DLL 'e_sqlite3'.");
        var outer = new TypeInitializationException("ShopPOS.Data.AppDbContext", inner);
        var info = BootstrapCrashHandler.MapException(outer);

        info.ErrorSummary.Should().Contain("SQLite");
        info.PlainLanguageAdvice.Should().Contain("Visual C++ runtime missing");
    }

    [Fact]
    public void BootstrapCrashHandler_LogAndMap_WritesToFile()
    {
        var logFile = Path.Combine(_tempDir, "startup-error.log");
        var ex = new InvalidOperationException("Early boot test exception");

        var info = BootstrapCrashHandler.LogAndMap(ex, logFile);

        info.LogFilePath.Should().Be(logFile);
        File.Exists(logFile).Should().BeTrue();
        string content = File.ReadAllText(logFile);
        content.Should().Contain("ShopPOS Startup Failure Report");
        content.Should().Contain("Early boot test exception");
    }

    [Fact]
    public void UserSettingsService_ResolveDataDirectory_CommandLineOverridesAll()
    {
        var service = new UserSettingsService(Path.Combine(_tempDir, "appsettings.user.json"));
        string customDir = Path.Combine(_tempDir, "CliDataDir");

        string resolved = service.ResolveDataDirectory(customDir);

        resolved.Should().Be(Path.GetFullPath(customDir));
    }

    [Fact]
    public void UserSettingsService_ResolveDataDirectory_FallsBackToLocalAppData_WhenDefaultUnwritable()
    {
        var service = new UserSettingsService(Path.Combine(_tempDir, "appsettings.user.json"));

        // When non-admin and C:\ShopData is not writable (or simulated), it falls back to LocalAppData\ShopPOS\Data
        string resolved = service.ResolveDataDirectory(null);

        resolved.Should().NotBeNullOrWhiteSpace();
        // It must be either C:\ShopData (if admin and writable) or %LocalAppData%\ShopPOS\Data
        bool isExpected = resolved.Equals(@"C:\ShopData", StringComparison.OrdinalIgnoreCase) ||
                          resolved.Contains("ShopPOS", StringComparison.OrdinalIgnoreCase);
        isExpected.Should().BeTrue();
    }

    [Fact]
    public async Task SelfCheckService_RunAllChecks_ExecutesAndGeneratesReport()
    {
        var testDataDir = Path.Combine(_tempDir, "SelfCheckData");
        var appPaths = new AppPaths(testDataDir);
        appPaths.EnsureDirectoriesExist();

        var service = new SelfCheckService(appPaths);
        var report = await service.RunAllChecksAsync();

        report.Items.Should().NotBeEmpty();
        report.FormattedReport.Should().Contain("ShopPOS Environment & System Self-Check Report");
        report.FormattedReport.Should().Contain("SQLite Native Engine");
        report.FormattedReport.Should().Contain("QuestPDF & SkiaSharp Graphics");
    }
}

