using System.Diagnostics;
using System.Text;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Repositories;
using ShopPOS.Data.Services;

namespace ShopPOS.Tests;

[Collection("PerformanceTests")]
[Trait("Category", "Slow")]
public class InventoryPerformanceTests
{
    [Fact]
    public async Task StockOverview_With20000Products_QueryShouldCompleteInUnder1Second()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"perf_overview_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            using (var initCtx = new AppDbContext(options))
            {
                await initCtx.Database.MigrateAsync();

                // Seed category and unit
                var cat = new Category { Name = "Bulk Category", IsActive = true, CreatedAt = DateTime.UtcNow };
                var unit = new Unit { Name = "PCS", ShortName = "pcs", IsActive = true, CreatedAt = DateTime.UtcNow };
                initCtx.Categories.Add(cat);
                initCtx.Units.Add(unit);
                await initCtx.SaveChangesAsync();

                int catId = cat.Id;
                int unitId = unit.Id;

                // Batched fast multi-row raw SQL insert for 20,000 products & opening stock movements
                using var conn = (SqliteConnection)initCtx.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var pragmaCmd = conn.CreateCommand())
                {
                    pragmaCmd.CommandText = "PRAGMA synchronous = OFF; PRAGMA journal_mode = MEMORY; PRAGMA cache_size = -200000; PRAGMA temp_store = MEMORY;";
                    await pragmaCmd.ExecuteNonQueryAsync();
                }

                using var trans = conn.BeginTransaction();
                var nowIso = DateTime.UtcNow.ToString("O");
                const int totalProducts = 20000;
                const int batchSize = 500;

                for (int b = 1; b <= totalProducts; b += batchSize)
                {
                    int bEnd = Math.Min(b + batchSize - 1, totalProducts);
                    var prodSb = new StringBuilder(10000);
                    prodSb.Append("INSERT INTO Products (Name, Sku, Barcode, CategoryId, UnitId, CostPrice, SalePrice, TaxPercent, ReorderLevel, TrackStock, IsActive, CreatedAt, IsDeleted) VALUES ");

                    var movSb = new StringBuilder(10000);
                    movSb.Append("INSERT INTO StockMovements (ProductId, MovementType, Quantity, UnitCost, Note, MovementDate, CreatedAt, IsDeleted) VALUES ");

                    for (int i = b; i <= bEnd; i++)
                    {
                        if (i > b)
                        {
                            prodSb.Append(',');
                            movSb.Append(',');
                        }
                        prodSb.Append($"('Product {i:D6}', 'SKU-{i:D6}', 'BAR-{i:D8}', {catId}, {unitId}, 1000, 1500, 0, 5000, 1, 1, '{nowIso}', 0)");
                        movSb.Append($"({i}, 6, 10000, 1000, 'Opening', '{nowIso}', '{nowIso}', 0)");
                    }
                    prodSb.Append(';');
                    movSb.Append(';');

                    using var pCmd = conn.CreateCommand();
                    pCmd.Transaction = trans;
                    pCmd.CommandText = prodSb.ToString();
                    pCmd.ExecuteNonQuery();

                    using var mCmd = conn.CreateCommand();
                    mCmd.Transaction = trans;
                    mCmd.CommandText = movSb.ToString();
                    mCmd.ExecuteNonQuery();
                }

                trans.Commit();
            }

            // Create InventoryService with a fresh DbContext instance
            using (var testCtx = new AppDbContext(options))
            {
                var ledgerQuery = new StockLedgerQuery(testCtx);
                var productRepo = new ProductRepository(testCtx);
                var categoryRepo = new CategoryRepository(testCtx);
                var brandRepo = new BrandRepository(testCtx);
                var settingRepo = new ShopSettingRepository(testCtx);
                var supplierLedgerRepo = new SupplierLedgerRepository(testCtx);
                var invRepo = new InventoryRepository(testCtx);
                var permService = new PermissionService();

                var invService = new InventoryService(
                    ledgerQuery,
                    productRepo,
                    categoryRepo,
                    brandRepo,
                    settingRepo,
                    supplierLedgerRepo,
                    invRepo,
                    new Mock<IPurchaseService>().Object,
                    permService,
                    new Mock<IPdfReportService>().Object);

                // Measure query execution time
                var sw = Stopwatch.StartNew();
                var result = await invService.GetStockOverviewAsync(new StockOverviewFilterQuery
                {
                    PageNumber = 1,
                    PageSize = 50
                }, RoleDefaults.Admin);
                sw.Stop();

                result.Success.Should().BeTrue();
                result.Data.Should().NotBeNull();
                result.Data!.TotalCount.Should().Be(20000);
                result.Data.Items.Should().HaveCount(50);
                sw.ElapsedMilliseconds.Should().BeLessThan(1000, $"20,000 products stock overview took {sw.ElapsedMilliseconds}ms, should complete in under 1 second");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task StockMovementHistory_With300000Movements_QueryShouldCompletePromptly()
    {
        const int totalMovements = 300000;
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"perf_movements_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            using (var initCtx = new AppDbContext(options))
            {
                await initCtx.Database.MigrateAsync();

                // Seed category, unit and 100 products
                var cat = new Category { Name = "Movement Category", IsActive = true, CreatedAt = DateTime.UtcNow };
                var unit = new Unit { Name = "PCS", ShortName = "pcs", IsActive = true, CreatedAt = DateTime.UtcNow };
                initCtx.Categories.Add(cat);
                initCtx.Units.Add(unit);
                await initCtx.SaveChangesAsync();

                using var conn = (SqliteConnection)initCtx.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var pragmaCmd = conn.CreateCommand())
                {
                    pragmaCmd.CommandText = "PRAGMA synchronous = OFF; PRAGMA journal_mode = MEMORY; PRAGMA cache_size = -200000; PRAGMA temp_store = MEMORY;";
                    await pragmaCmd.ExecuteNonQueryAsync();
                }

                // Insert 100 products
                using (var trans = conn.BeginTransaction())
                {
                    var nowIso = DateTime.UtcNow.ToString("O");
                    var prodSb = new StringBuilder(10000);
                    prodSb.Append("INSERT INTO Products (Name, Sku, Barcode, CategoryId, UnitId, CostPrice, SalePrice, TaxPercent, ReorderLevel, TrackStock, IsActive, CreatedAt, IsDeleted) VALUES ");

                    for (int i = 1; i <= 100; i++)
                    {
                        if (i > 1) prodSb.Append(',');
                        prodSb.Append($"('Product {i:D3}', 'SKU-{i:D3}', 'BAR-{i:D6}', {cat.Id}, {unit.Id}, 1000, 1500, 0, 5000, 1, 1, '{nowIso}', 0)");
                    }
                    prodSb.Append(';');

                    using var prodCmd = conn.CreateCommand();
                    prodCmd.Transaction = trans;
                    prodCmd.CommandText = prodSb.ToString();
                    await prodCmd.ExecuteNonQueryAsync();
                    await trans.CommitAsync();
                }

                // Insert 300,000 movements in multi-row batches of 1,000 inside a single transaction
                const int batchSize = 1000;
                var nowIsoDate = DateTime.UtcNow.ToString("O");

                using (var trans = conn.BeginTransaction())
                {
                    for (int batchStart = 1; batchStart <= totalMovements; batchStart += batchSize)
                    {
                        int batchEnd = Math.Min(batchStart + batchSize - 1, totalMovements);
                        var movSb = new StringBuilder(25000);
                        movSb.Append("INSERT INTO StockMovements (ProductId, MovementType, Quantity, UnitCost, Note, MovementDate, CreatedAt, IsDeleted) VALUES ");

                        for (int i = batchStart; i <= batchEnd; i++)
                        {
                            if (i > batchStart) movSb.Append(',');
                            int prodId = (i % 100) + 1; // distribute across the 100 products
                            movSb.Append($"({prodId}, 1, 10000, 1000, 'Purchase', '{nowIsoDate}', '{nowIsoDate}', 0)");
                        }
                        movSb.Append(';');

                        using var movCmd = conn.CreateCommand();
                        movCmd.Transaction = trans;
                        movCmd.CommandText = movSb.ToString();
                        movCmd.ExecuteNonQuery();
                    }
                    trans.Commit();
                }
            }

            // Create InventoryService with a fresh DbContext instance
            using (var testCtx = new AppDbContext(options))
            {
                var ledgerQuery = new StockLedgerQuery(testCtx);
                var productRepo = new ProductRepository(testCtx);
                var categoryRepo = new CategoryRepository(testCtx);
                var brandRepo = new BrandRepository(testCtx);
                var settingRepo = new ShopSettingRepository(testCtx);
                var supplierLedgerRepo = new SupplierLedgerRepository(testCtx);
                var invRepo = new InventoryRepository(testCtx);
                var permService = new PermissionService();

                var invService = new InventoryService(
                    ledgerQuery,
                    productRepo,
                    categoryRepo,
                    brandRepo,
                    settingRepo,
                    supplierLedgerRepo,
                    invRepo,
                    new Mock<IPurchaseService>().Object,
                    permService,
                    new Mock<IPdfReportService>().Object);

                // 1. Measure paged overall movement history
                var sw = Stopwatch.StartNew();
                var result = await invService.GetMovementHistoryAsync(new MovementHistoryFilterQuery
                {
                    PageNumber = 1,
                    PageSize = 50
                }, RoleDefaults.Admin);
                sw.Stop();

                result.Success.Should().BeTrue();
                result.Data.Should().NotBeNull();
                result.Data!.TotalCount.Should().Be(totalMovements);
                result.Data.Items.Should().HaveCount(50);
                sw.ElapsedMilliseconds.Should().BeLessThan(2000, $"300,000 movements paged history took {sw.ElapsedMilliseconds}ms, should complete promptly");

                // 2. Measure single product movement history (running balances)
                var swProduct = Stopwatch.StartNew();
                var productResult = await invService.GetMovementHistoryAsync(new MovementHistoryFilterQuery
                {
                    ProductId = 1,
                    PageNumber = 1,
                    PageSize = 50
                }, RoleDefaults.Admin);
                swProduct.Stop();

                productResult.Success.Should().BeTrue();
                productResult.Data!.TotalCount.Should().Be(totalMovements / 100);
                productResult.Data.Items.Should().HaveCount(50);
                swProduct.ElapsedMilliseconds.Should().BeLessThan(1000, $"Single product movement history took {swProduct.ElapsedMilliseconds}ms, should complete in under 1 second");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
