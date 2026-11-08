using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using Xunit;
using Xunit.Abstractions;

namespace ShopPOS.Tests;

public class BackupPerformanceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly IAppPaths _appPaths;
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _context;
    private readonly IBackupService _backupService;
    private readonly IRestoreService _restoreService;
    private readonly ITestOutputHelper _output;

    public BackupPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _testRoot = Path.Combine(Path.GetTempPath(), $"BackupPerf_{Guid.NewGuid():N}");
        _appPaths = new AppPaths(_testRoot);
        _appPaths.EnsureDirectoriesExist();

        var services = new ServiceCollection();
        services.AddSingleton<IAppPaths>(_appPaths);
        services.AddCoreServices();
        services.AddDataServices(new DatabaseOptions { DbPath = _appPaths.DatabasePath });

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<AppDbContext>();
        _context.Database.Migrate();

        _backupService = _serviceProvider.GetRequiredService<IBackupService>();
        _restoreService = _serviceProvider.GetRequiredService<IRestoreService>();

        _context.ShopSettings.Add(new ShopSetting { ShopName = "Perf Mart" });
        _context.SaveChanges();
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task LargeScale_100kSales_1Mines_BackupAndRestorePerformance()
    {
        _output.WriteLine("Seeding high-volume dataset directly via raw batched SQLite inserts...");
        var sw = Stopwatch.StartNew();

        using (var conn = new SqliteConnection($"Data Source={_appPaths.DatabasePath}"))
        {
            await conn.OpenAsync();
            using var pragmaCmd = conn.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA synchronous = OFF; PRAGMA journal_mode = MEMORY;";
            await pragmaCmd.ExecuteNonQueryAsync();

            using var tx = conn.BeginTransaction();

            using (var catCmd = conn.CreateCommand())
            {
                catCmd.Transaction = tx;
                catCmd.CommandText = "INSERT OR IGNORE INTO \"Categories\" (Id, Name, CreatedAt, IsDeleted) VALUES (1, 'General', '2026-01-01', 0);";
                await catCmd.ExecuteNonQueryAsync();
            }
            using (var unitCmd = conn.CreateCommand())
            {
                unitCmd.Transaction = tx;
                unitCmd.CommandText = "INSERT OR IGNORE INTO \"Units\" (Id, Name, ShortName, CreatedAt, IsDeleted) VALUES (1, 'Piece', 'pc', '2026-01-01', 0);";
                await unitCmd.ExecuteNonQueryAsync();
            }

            // Insert 100 sample products
            for (int p = 1; p <= 100; p++)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = $"INSERT INTO \"Products\" (Id, Name, Sku, CostPrice, SalePrice, ReorderLevel, TaxPercent, TrackStock, IsActive, CategoryId, UnitId, SearchText, CreatedAt, IsDeleted) VALUES ({p}, 'Product {p}', 'SKU-{p:D4}', 1000, 1500, 0, 0, 1, 1, 1, 1, 'product {p} sku-{p:D4}', '2026-01-01', 0);";
                await cmd.ExecuteNonQueryAsync();
            }

            // Insert 100,000 sales and 1,000,000 sale lines (10 items per sale) in batches of 2,000
            int lineId = 1;
            for (int batch = 0; batch < 50; batch++)
            {
                var salesBatch = new System.Text.StringBuilder();
                var itemsBatch = new System.Text.StringBuilder();

                for (int s = 1; s <= 2000; s++)
                {
                    int saleId = (batch * 2000) + s;
                    salesBatch.AppendLine($"INSERT INTO \"Sales\" (Id, SaleNumber, SaleDate, Status, SubTotal, LineDiscountTotal, BillDiscountAmount, TaxAmount, RoundOff, GrandTotal, PaidAmount, ChangeAmount, DueAmount, TotalCost, CreatedAt, IsDeleted) VALUES ({saleId}, 'INV-{saleId:D7}', '2026-06-01', 1, 1500000, 0, 0, 0, 0, 1500000, 1500000, 0, 0, 1000000, '2026-06-01', 0);");

                    for (int item = 1; item <= 10; item++)
                    {
                        int prodId = ((item * 7) % 100) + 1;
                        itemsBatch.AppendLine($"INSERT INTO \"SaleItems\" (Id, SaleId, ProductId, ProductName, Sku, Quantity, UnitPrice, CostPriceSnapshot, DiscountPercent, DiscountAmount, TaxPercent, LineTotal, LineNo, CreatedAt, IsDeleted) VALUES ({lineId++}, {saleId}, {prodId}, 'Product {prodId}', 'SKU-{prodId:D4}', 1000, 150000, 100000, 0, 0, 0, 150000, {item}, '2026-06-01', 0);");
                    }
                }

                using var cmdSales = conn.CreateCommand();
                cmdSales.Transaction = tx;
                cmdSales.CommandText = salesBatch.ToString();
                await cmdSales.ExecuteNonQueryAsync();

                using var cmdItems = conn.CreateCommand();
                cmdItems.Transaction = tx;
                cmdItems.CommandText = itemsBatch.ToString();
                await cmdItems.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }

        sw.Stop();
        _output.WriteLine($"Database seeded with 100,000 sales and 1,000,000 lines in {sw.ElapsedMilliseconds} ms.");

        // Benchmark Backup
        var backupSw = Stopwatch.StartNew();
        var backupResult = await _backupService.CreateBackupAsync(BackupKind.Manual);
        backupSw.Stop();

        _output.WriteLine($"Backup completed in {backupSw.ElapsedMilliseconds} ms. Size: {backupResult.SizeBytes / (1024 * 1024.0):F2} MB. Path: {backupResult.FileName}");
        backupResult.Success.Should().BeTrue();

        // Benchmark Restore
        await _context.Database.CloseConnectionAsync();
        var restoreSw = Stopwatch.StartNew();
        var restoreResult = await _restoreService.RestoreBackupAsync(backupResult.FilePath!);
        restoreSw.Stop();

        _output.WriteLine($"Restore completed in {restoreSw.ElapsedMilliseconds} ms.");
        restoreResult.Success.Should().BeTrue();
        restoreResult.RestoredSales.Should().Be(100000);
    }

    public void Dispose()
    {
        _context.Dispose();
        _serviceProvider.Dispose();
        try { Directory.Delete(_testRoot, recursive: true); } catch { }
    }
}

