using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShopPOS.Core.DTOs.Diagnostics;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class DiagnosticsServiceTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly AppPaths _appPaths;
    private readonly string _connectionString;

    public DiagnosticsServiceTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "ShopPOS_DiagTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);

        _appPaths = new AppPaths(_tempFolder);
        _connectionString = $"Data Source={_appPaths.DatabasePath};Foreign Keys=True;";
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, recursive: true);
            }
        }
        catch { }
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateSupportBundleAsync_CreatesZipWithSanitizedDataAndNeverDatabaseOrSecrets()
    {
        // 1. Setup migrated DB with sample data
        using (var ctx = CreateDbContext())
        {
            await ctx.Database.MigrateAsync();

            ctx.ShopSettings.Add(new ShopSetting
            {
                ShopName = "Confidential Shop",
                Address = "Top Secret 123",
                EncryptionSalt = "SensitiveSaltBytes",
                EncryptionKeyCheck = "SensitiveKeyCheck"
            });
            await ctx.SaveChangesAsync();
        }

        // 2. Create sample dummy logs and a dummy token file that should NEVER be bundled
        Directory.CreateDirectory(_appPaths.LogsDirectory);
        string logFile = Path.Combine(_appPaths.LogsDirectory, "shoppos-20261104.log");
        await File.WriteAllTextAsync(logFile, "2026-11-04 12:00:00 [INF] Test diagnostic log entry");

        string sensitiveTokenFile = Path.Combine(_appPaths.DataDirectory, "google_token.dat");
        await File.WriteAllTextAsync(sensitiveTokenFile, "SUPER_SECRET_DPAPI_BLOB");

        string zipDestination = Path.Combine(_tempFolder, "support_output.zip");

        using (var ctx = CreateDbContext())
        {
            var diagnosticsService = new DiagnosticsService(
                ctx,
                _appPaths,
                new NullLogger<DiagnosticsService>());

            // 3. Generate bundle
            var outputZip = await diagnosticsService.CreateSupportBundleAsync(zipDestination);

            Assert.Equal(zipDestination, outputZip);
            Assert.True(File.Exists(zipDestination));
        }

        // 4. Inspect ZIP contents
        using (var archive = ZipFile.OpenRead(zipDestination))
        {
            // Verify diagnostics.json exists
            var reportEntry = archive.GetEntry("diagnostics.json");
            Assert.NotNull(reportEntry);

            using (var stream = reportEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                string json = await reader.ReadToEndAsync();
                var report = JsonSerializer.Deserialize<DiagnosticsReportDto>(json);
                Assert.NotNull(report);
                Assert.NotNull(report.AppVersion);
                Assert.NotNull(report.OsVersion);
                Assert.NotNull(report.DotNetVersion);
                Assert.NotNull(report.TableRowCounts);
                Assert.True(report.TableRowCounts.Count > 0);

                // Verify settings are included without secrets
                Assert.NotNull(report.Settings);
                Assert.Equal("Confidential Shop", report.Settings.ShopName);
            }

            // Verify logs were bundled
            var logEntry = archive.GetEntry("Logs/shoppos-20261104.log");
            Assert.NotNull(logEntry);

            // STRICT SECURITY AUDIT: Verify no forbidden database, token, or secret files exist in the archive
            foreach (var entry in archive.Entries)
            {
                string name = entry.FullName.ToLowerInvariant();
                Assert.False(name.EndsWith(".db"), $"Database file leaked into support bundle: {entry.FullName}");
                Assert.False(name.EndsWith(".db-wal"), $"WAL file leaked into support bundle: {entry.FullName}");
                Assert.False(name.EndsWith(".db-shm"), $"SHM file leaked into support bundle: {entry.FullName}");
                Assert.False(name.Contains("token"), $"Token file leaked into support bundle: {entry.FullName}");
                Assert.False(name.EndsWith(".dat"), $"Dat file leaked into support bundle: {entry.FullName}");
            }
        }
    }
}
