using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.DTOs.Diagnostics;
using ShopPOS.Core.Interfaces;
using ShopPOS.Data;

namespace ShopPOS.Data.Services;

/// <summary>
/// Packages a sanitized support diagnostic bundle for troubleshooting without leaking sensitive data.
/// </summary>
public class DiagnosticsService : IDiagnosticsService
{
    private readonly AppDbContext _dbContext;
    private readonly IAppPaths _appPaths;
    private readonly ILogger<DiagnosticsService> _logger;

    public DiagnosticsService(
        AppDbContext dbContext,
        IAppPaths appPaths,
        ILogger<DiagnosticsService>? logger = null)
    {
        _dbContext = dbContext;
        _appPaths = appPaths;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DiagnosticsService>.Instance;
    }

    public async Task<DiagnosticsReportDto> GenerateReportAsync(CancellationToken cancellationToken = default)
    {
        var report = new DiagnosticsReportDto
        {
            AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "1.0.0",
            OsVersion = RuntimeInformation.OSDescription,
            DotNetVersion = RuntimeInformation.FrameworkDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            GeneratedAtLocal = DateTime.Now,
            DataDirectory = _appPaths.DataDirectory
        };

        // Database size
        try
        {
            if (File.Exists(_appPaths.DatabasePath))
            {
                var fi = new FileInfo(_appPaths.DatabasePath);
                report.DatabaseSizeBytes = fi.Length;
            }
        }
        catch { }

        // Schema version (applied migrations)
        try
        {
            var migrations = await _dbContext.Database.GetAppliedMigrationsAsync(cancellationToken);
            report.DatabaseSchemaVersion = migrations.LastOrDefault() ?? "None";
        }
        catch (Exception ex)
        {
            report.DatabaseSchemaVersion = $"Error reading migrations: {ex.Message}";
        }

        // Table row counts
        try
        {
            report.TableRowCounts["Products"] = await _dbContext.Products.CountAsync(cancellationToken);
            report.TableRowCounts["Categories"] = await _dbContext.Categories.CountAsync(cancellationToken);
            report.TableRowCounts["Brands"] = await _dbContext.Brands.CountAsync(cancellationToken);
            report.TableRowCounts["Suppliers"] = await _dbContext.Suppliers.CountAsync(cancellationToken);
            report.TableRowCounts["Units"] = await _dbContext.Units.CountAsync(cancellationToken);
            report.TableRowCounts["Sales"] = await _dbContext.Sales.CountAsync(cancellationToken);
            report.TableRowCounts["Purchases"] = await _dbContext.Purchases.CountAsync(cancellationToken);
            report.TableRowCounts["Expenses"] = await _dbContext.Expenses.CountAsync(cancellationToken);
            report.TableRowCounts["StockAdjustments"] = await _dbContext.StockAdjustments.CountAsync(cancellationToken);
            report.TableRowCounts["AuditLogs"] = await _dbContext.AuditLogs.CountAsync(cancellationToken);
            report.TableRowCounts["BackupRecords"] = await _dbContext.BackupRecords.CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query row counts for diagnostics.");
        }

        // Settings (sanitized)
        try
        {
            var s = await _dbContext.ShopSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (s != null)
            {
                report.Settings = new SanitizedSettingsDto
                {
                    ShopName = s.ShopName,
                    CurrencySymbol = s.CurrencySymbol,
                    CurrencyDecimals = s.CurrencyDecimals,
                    DefaultTaxPercent = s.DefaultTaxPercent,
                    PricesIncludeTax = s.PricesIncludeTax,
                    CashRoundingStep = s.CashRoundingStep,
                    ReceiptWidthMm = s.ReceiptWidthMm,
                    PrinterMode = s.PrinterMode.ToString(),
                    AutoPrintReceipt = s.AutoPrintReceipt,
                    BackupEnabled = s.BackupEnabled,
                    BackupTime = s.BackupTime,
                    BackupOnAppClose = s.BackupOnAppClose,
                    KeepDaily = s.KeepDaily,
                    KeepWeekly = s.KeepWeekly,
                    KeepMonthly = s.KeepMonthly,
                    EncryptBackups = s.EncryptBackups,
                    HasSecondFolder = !string.IsNullOrWhiteSpace(s.SecondFolderPath),
                    GoogleDriveEnabled = s.GoogleDriveEnabled,
                    IsGoogleDriveConnected = !string.IsNullOrWhiteSpace(s.GoogleDriveAccountEmail)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load shop settings for diagnostics.");
        }

        return report;
    }

    public async Task<string> CreateSupportBundleAsync(string? destinationZipPath = null, CancellationToken cancellationToken = default)
    {
        var report = await GenerateReportAsync(cancellationToken);

        string zipPath = destinationZipPath ?? Path.Combine(
            _appPaths.TempDirectory,
            $"ShopPOS_Support_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        string? dir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            // 1. Add diagnostics.json
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            string reportJson = JsonSerializer.Serialize(report, jsonOptions);
            var jsonEntry = zip.CreateEntry("diagnostics.json", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(jsonEntry.Open()))
            {
                await writer.WriteAsync(reportJson.AsMemory(), cancellationToken);
            }

            // 1b. Add self-check report if exists
            string selfCheckLogs = Path.Combine(_appPaths.LogsDirectory, "self-check-report.txt");
            string selfCheckLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShopPOS", "self-check-report.txt");
            string? selfCheckSource = File.Exists(selfCheckLogs) ? selfCheckLogs : (File.Exists(selfCheckLocal) ? selfCheckLocal : null);
            if (selfCheckSource != null)
            {
                zip.CreateEntryFromFile(selfCheckSource, "self-check-report.txt", CompressionLevel.Optimal);
            }

            // 1c. Add startup-error.log if exists
            string startupLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShopPOS", "startup-error.log");
            if (File.Exists(startupLog))
            {
                using var fs = new FileStream(startupLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var entry = zip.CreateEntry("startup-error.log", CompressionLevel.Optimal);
                using var es = entry.Open();
                await fs.CopyToAsync(es, cancellationToken);
            }

            // 2. Add last 7 days of logs
            if (Directory.Exists(_appPaths.LogsDirectory))
            {
                var cutoffDate = DateTime.Now.Date.AddDays(-7);
                var logFiles = Directory.GetFiles(_appPaths.LogsDirectory, "shoppos-*.log")
                    .Concat(Directory.GetFiles(_appPaths.LogsDirectory, "app-*.log"))
                    .Distinct();

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.LastWriteTime >= cutoffDate)
                    {
                        // Open with FileShare.ReadWrite so active log can be read
                        using var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        var entry = zip.CreateEntry($"Logs/{fileInfo.Name}", CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        await fileStream.CopyToAsync(entryStream, cancellationToken);
                    }
                }
            }
        }

        // Strict security assertion: Verify zip does not contain shop.db, tokens or sqlite files
        using (var verifyZip = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in verifyZip.Entries)
            {
                string name = entry.Name.ToLowerInvariant();
                if (name.EndsWith(".db") || name.EndsWith(".db-wal") || name.EndsWith(".db-shm") || name.Contains("token"))
                {
                    File.Delete(zipPath);
                    throw new InvalidOperationException($"Security violation: Sensitive file '{entry.FullName}' detected in support bundle.");
                }
            }
        }

        _logger.LogInformation("Support diagnostic bundle created at '{ZipPath}'", zipPath);
        return zipPath;
    }
}

