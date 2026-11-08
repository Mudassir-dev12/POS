#pragma warning disable CA1416
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShopPOS.Core.Interfaces;
using ShopPOS.Data;

namespace ShopPOS.Data.Services;

public class SelfCheckService : ISelfCheckService
{
    private readonly AppDbContext? _dbContext;
    private readonly IAppPaths _appPaths;
    private readonly IBackupService? _backupService;

    public SelfCheckService(
        IAppPaths appPaths,
        AppDbContext? dbContext = null,
        IBackupService? backupService = null)
    {
        _appPaths = appPaths;
        _dbContext = dbContext;
        _backupService = backupService;
    }

    public async Task<SelfCheckReport> RunAllChecksAsync(CancellationToken cancellationToken = default)
    {
        var report = new SelfCheckReport();

        // 1. Architecture Check (x64)
        report.Items.Add(CheckArchitecture());

        // 2. .NET Runtime Version
        report.Items.Add(CheckDotNetRuntime());

        // 3. SQLite Native Library (e_sqlite3.dll) & Engine
        report.Items.Add(await CheckSqliteNativeAsync());

        // 4. QuestPDF & SkiaSharp Vector Engine
        report.Items.Add(CheckQuestPdfAndSkia());

        // 5. Data Directory Writable & Free Disk Space
        report.Items.Add(CheckDataDirectoryAndSpace());

        // 6. Printer Subsystem (Print Spooler)
        report.Items.Add(CheckPrinters());

        // 7. Font Subsystem
        report.Items.Add(CheckFonts());

        // 8. Database Schema & Migration Status
        report.Items.Add(await CheckDatabaseSchemaAsync(cancellationToken));

        // 9. In-Memory Database Backup Engine
        report.Items.Add(await CheckBackupEngineAsync(cancellationToken));

        // Format Report
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("ShopPOS Environment & System Self-Check Report");
        sb.AppendLine($"Generated: {report.RunAt:yyyy-MM-dd HH:mm:ss} | Overall: {(report.AllPassed ? "PASS" : "FAIL")}");
        sb.AppendLine("================================================================================");

        foreach (var item in report.Items)
        {
            string status = item.Passed ? "[PASS]" : "[FAIL]";
            sb.AppendLine($"{status,-7} {item.Name}: {item.Details}");
            if (!item.Passed && !string.IsNullOrWhiteSpace(item.FailureReason))
            {
                sb.AppendLine($"        Reason: {item.FailureReason}");
            }
        }
        sb.AppendLine("================================================================================");

        report.FormattedReport = sb.ToString();

        // Save report to logs folder and %LocalAppData%\ShopPOS\
        TrySaveReport(report.FormattedReport);

        return report;
    }

    private static SelfCheckItemResult CheckArchitecture()
    {
        bool is64 = Environment.Is64BitProcess;
        string osArch = RuntimeInformation.OSArchitecture.ToString();
        string procArch = RuntimeInformation.ProcessArchitecture.ToString();

        if (is64)
        {
            return new SelfCheckItemResult(
                "64-Bit Architecture",
                true,
                $"Process is 64-bit ({procArch}) on 64-bit OS ({osArch}).");
        }

        return new SelfCheckItemResult(
            "64-Bit Architecture",
            false,
            $"Process is 32-bit ({procArch}). ShopPOS requires 64-bit runtime execution.",
            "32-bit process architecture is not supported.");
    }

    private static SelfCheckItemResult CheckDotNetRuntime()
    {
        string framework = RuntimeInformation.FrameworkDescription;
        Version clrVer = Environment.Version;

        bool isNet10OrGreater = clrVer.Major >= 10 || framework.Contains(".NET 10", StringComparison.OrdinalIgnoreCase);

        return new SelfCheckItemResult(
            ".NET Desktop Runtime",
            isNet10OrGreater,
            $"{framework} (CLR v{clrVer})",
            isNet10OrGreater ? null : "Target requires .NET 10 or greater.");
    }

    private static async Task<SelfCheckItemResult> CheckSqliteNativeAsync()
    {
        try
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT sqlite_version();";
            var ver = await cmd.ExecuteScalarAsync();

            return new SelfCheckItemResult(
                "SQLite Native Engine",
                true,
                $"SQLite version {ver} loaded successfully via e_sqlite3 native library.");
        }
        catch (Exception ex)
        {
            return new SelfCheckItemResult(
                "SQLite Native Engine",
                false,
                "Failed to initialize SQLite native library.",
                ex.Message);
        }
    }

    private static SelfCheckItemResult CheckQuestPdfAndSkia()
    {
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseSystemFonts = true;
            QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Content().Text("ShopPOS Diagnostic Self-Check Page");
                });
            });

            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);

            if (ms.Length > 0)
            {
                return new SelfCheckItemResult(
                    "QuestPDF & SkiaSharp Graphics",
                    true,
                    $"Vector engine generated test PDF successfully ({ms.Length} bytes).");
            }

            return new SelfCheckItemResult(
                "QuestPDF & SkiaSharp Graphics",
                false,
                "PDF output generated 0 bytes.",
                "Generated PDF was empty.");
        }
        catch (Exception ex)
        {
            return new SelfCheckItemResult(
                "QuestPDF & SkiaSharp Graphics",
                false,
                "QuestPDF / SkiaSharp vector graphics engine failed.",
                ex.Message);
        }
    }

    private SelfCheckItemResult CheckDataDirectoryAndSpace()
    {
        try
        {
            string dir = _appPaths.DataDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string testFile = Path.Combine(dir, $".selfcheck_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            string? root = Path.GetPathRoot(Path.GetFullPath(dir));
            long freeBytes = 0;
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                freeBytes = drive.AvailableFreeSpace;
            }

            double freeGb = freeBytes / (1024.0 * 1024.0 * 1024.0);
            bool spaceOk = freeBytes >= (500L * 1024 * 1024); // 500 MB minimum

            if (spaceOk)
            {
                return new SelfCheckItemResult(
                    "Data Directory & Storage",
                    true,
                    $"{dir} (Writable, {freeGb:F1} GB free space available).");
            }

            return new SelfCheckItemResult(
                "Data Directory & Storage",
                false,
                $"{dir} (Writable, only {freeGb:F2} GB free space available).",
                "Less than 500 MB free space available.");
        }
        catch (Exception ex)
        {
            return new SelfCheckItemResult(
                "Data Directory & Storage",
                false,
                $"Data directory '{_appPaths.DataDirectory}' is not accessible or writable.",
                ex.Message);
        }
    }

    private static SelfCheckItemResult CheckPrinters()
    {
        try
        {
            var printers = new List<string>();
            foreach (string p in PrinterSettings.InstalledPrinters)
            {
                printers.Add(p);
            }

            string details = printers.Count > 0
                ? $"{printers.Count} installed printer(s) detected ({string.Join(", ", printers.Take(3))}{(printers.Count > 3 ? "..." : "")})."
                : "Print spooler operational (0 physical printers installed).";

            return new SelfCheckItemResult(
                "Printer Subsystem",
                true,
                details);
        }
        catch (Exception ex)
        {
            return new SelfCheckItemResult(
                "Printer Subsystem",
                false,
                "Windows Print Spooler could not be queried.",
                ex.Message);
        }
    }

    private static SelfCheckItemResult CheckFonts()
    {
        try
        {
            var families = System.Drawing.FontFamily.Families;
            var names = families.Select(f => f.Name).ToList();
            bool hasStandardFont = names.Any(n => n.Contains("Arial", StringComparison.OrdinalIgnoreCase) ||
                                                  n.Contains("Segoe", StringComparison.OrdinalIgnoreCase) ||
                                                  n.Contains("Calibri", StringComparison.OrdinalIgnoreCase));

            return new SelfCheckItemResult(
                "Font Subsystem",
                hasStandardFont && families.Length > 0,
                $"{families.Length} system font families loaded.",
                hasStandardFont ? null : "Standard fonts (Arial, Segoe UI, Calibri) not found.");
        }
        catch (Exception ex)
        {
            return new SelfCheckItemResult(
                "Font Subsystem",
                false,
                "Failed to query system font families.",
                ex.Message);
        }
    }

    private async Task<SelfCheckItemResult> CheckDatabaseSchemaAsync(CancellationToken cancellationToken)
    {
        if (_dbContext == null)
        {
            return new SelfCheckItemResult(
                "Database Engine & Schema",
                true,
                "Database context omitted for standalone test probe.");
        }

        try
        {
            // Apply migrations to ensure schema is initialized if brand new directory
            await _dbContext.Database.MigrateAsync(cancellationToken);

            bool canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return new SelfCheckItemResult(
                    "Database Engine & Schema",
                    false,
                    $"Cannot connect to database file at '{_appPaths.DatabasePath}'.",
                    "Database connection failed.");
            }

            var migrations = (await _dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            return new SelfCheckItemResult(
                "Database Engine & Schema",
                true,
                $"Connected to shop.db ({migrations.Count} migrations applied; latest: {migrations.LastOrDefault() ?? "Initial"}).");
        }
        catch (Exception ex)
        {
            return new SelfCheckItemResult(
                "Database Engine & Schema",
                false,
                $"Database probe error on '{_appPaths.DatabasePath}'.",
                ex.Message);
        }
    }

    private static async Task<SelfCheckItemResult> CheckBackupEngineAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var src = new SqliteConnection("Data Source=:memory:;");
            await src.OpenAsync(cancellationToken);

            using var cmd = src.CreateCommand();
            cmd.CommandText = "CREATE TABLE TestBackup (Id INT PRIMARY KEY, Val TEXT); INSERT INTO TestBackup VALUES (1, 'BackupEngineTest');";
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            using var dest = new SqliteConnection("Data Source=:memory:;");
            await dest.OpenAsync(cancellationToken);

            src.BackupDatabase(dest);

            using var verifyCmd = dest.CreateCommand();
            verifyCmd.CommandText = "SELECT Val FROM TestBackup WHERE Id = 1;";
            var val = await verifyCmd.ExecuteScalarAsync(cancellationToken);

            if (val?.ToString() == "BackupEngineTest")
            {
                return new SelfCheckItemResult(
                    "Database Backup Engine",
                    true,
                    "SQLite online streaming backup engine verified operational.");
            }

            return new SelfCheckItemResult(
                "Database Backup Engine",
                false,
                "Backup database verification query did not match expected value.",
                "Data verification failed during streaming backup.");
        }
        catch (Exception ex)
        {
            return new SelfCheckItemResult(
                "Database Backup Engine",
                false,
                "Failed to run in-memory streaming backup test.",
                ex.Message);
        }
    }

    private void TrySaveReport(string formattedReport)
    {
        try
        {
            // 1. Save in Logs directory
            if (!Directory.Exists(_appPaths.LogsDirectory))
            {
                Directory.CreateDirectory(_appPaths.LogsDirectory);
            }
            string logPath = Path.Combine(_appPaths.LogsDirectory, "self-check-report.txt");
            File.WriteAllText(logPath, formattedReport);
        }
        catch { }

        try
        {
            // 2. Save in %LocalAppData%\ShopPOS\
            string localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShopPOS");
            if (!Directory.Exists(localAppDir))
            {
                Directory.CreateDirectory(localAppDir);
            }
            string localPath = Path.Combine(localAppDir, "self-check-report.txt");
            File.WriteAllText(localPath, formattedReport);
        }
        catch { }
    }
}
#pragma warning restore CA1416
