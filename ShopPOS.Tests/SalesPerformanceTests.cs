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

namespace ShopPOS.Tests;

[Collection("PerformanceTests")]
[Trait("Category", "Slow")]
public class SalesPerformanceTests
{
    [Fact]
    public async Task TodaySalesList_AndShiftSummary_With5000SalesAnd50000Lines_CompleteUnder500ms()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"perf_sales_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            int shiftId;
            using (var initCtx = new AppDbContext(options))
            {
                await initCtx.Database.MigrateAsync();

                var role = new Role { Name = "Owner" };
                initCtx.Roles.Add(role);

                var user = new User
                {
                    Username = "owner",
                    FullName = "Owner",
                    Role = role,
                    IsActive = true
                };
                initCtx.Users.Add(user);

                var cat = new Category { Name = "Bulk Category", IsActive = true, CreatedAt = DateTime.UtcNow };
                var unit = new Unit { Name = "PCS", ShortName = "pcs", IsActive = true, CreatedAt = DateTime.UtcNow };
                initCtx.Categories.Add(cat);
                initCtx.Units.Add(unit);

                var customer = new Customer { Name = "Walk-in Retail Customer", Phone = "555-0000", IsActive = true };
                initCtx.Customers.Add(customer);

                var shift = new Shift
                {
                    ShiftNumber = "SHF-0001",
                    OpenedAt = DateTime.Today.AddHours(8),
                    OpeningCash = 100m,
                    Status = ShiftStatus.Open,
                    CreatedByUserId = 1
                };
                initCtx.Shifts.Add(shift);

                await initCtx.SaveChangesAsync();
                shiftId = shift.Id;

                int catId = cat.Id;
                int unitId = unit.Id;
                int customerId = customer.Id;

                using var conn = (SqliteConnection)initCtx.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var pragmaCmd = conn.CreateCommand())
                {
                    pragmaCmd.CommandText = "PRAGMA synchronous = OFF; PRAGMA journal_mode = MEMORY; PRAGMA cache_size = -200000; PRAGMA temp_store = MEMORY;";
                    await pragmaCmd.ExecuteNonQueryAsync();
                }

                using var trans = conn.BeginTransaction();
                var nowIso = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Insert 10 products
                var prodSb = new StringBuilder();
                prodSb.Append("INSERT INTO Products (Id, Name, Sku, Barcode, CategoryId, UnitId, CostPrice, SalePrice, TaxPercent, ReorderLevel, TrackStock, IsActive, CreatedAt, IsDeleted) VALUES ");
                for (int p = 1; p <= 10; p++)
                {
                    if (p > 1) prodSb.Append(',');
                    prodSb.Append($"({p}, 'Product {p}', 'SKU-{p:D4}', 'BAR-{p:D6}', {catId}, {unitId}, 500, 1000, 0, 1000, 1, 1, '{nowIso}', 0)");
                }
                prodSb.Append(';');
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = trans;
                    cmd.CommandText = prodSb.ToString();
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert 5,000 sales with 10 lines each (50,000 lines) in batches of 500 sales
                const int totalSales = 5000;
                const int batchSize = 500;

                int lineIdCounter = 1;
                for (int b = 1; b <= totalSales; b += batchSize)
                {
                    int bEnd = Math.Min(b + batchSize - 1, totalSales);

                    var saleSb = new StringBuilder();
                    saleSb.Append("INSERT INTO Sales (Id, SaleNumber, HoldNumber, SaleDate, CustomerId, PriceLevelId, Status, SubTotal, LineDiscountTotal, BillDiscountAmount, TaxAmount, RoundOff, GrandTotal, PaidAmount, ChangeAmount, DueAmount, TotalCost, ShiftId, CompletedAt, IsDeleted, CreatedAt, UpdatedAt, CreatedByUserId) VALUES ");

                    var itemSb = new StringBuilder();
                    itemSb.Append("INSERT INTO SaleItems (Id, SaleId, ProductId, ProductName, Sku, Barcode, Quantity, UnitPrice, CostPriceSnapshot, DiscountPercent, DiscountAmount, TaxPercent, LineTotal, LineNo, IsDeleted, CreatedAt, UpdatedAt) VALUES ");

                    var paySb = new StringBuilder();
                    paySb.Append("INSERT INTO SalePayments (Id, SaleId, Method, Amount, TenderedAmount, Reference, PaidAt, IsDeleted, CreatedAt, UpdatedAt, CreatedByUserId) VALUES ");

                    bool firstSale = true;
                    bool firstItem = true;
                    bool firstPay = true;

                    for (int s = b; s <= bEnd; s++)
                    {
                        if (!firstSale) saleSb.Append(',');
                        firstSale = false;

                        // Subtotal 10000 ($100), GrandTotal 10000, Paid 10000, Cost 5000
                        saleSb.Append($"({s}, 'INV-{s:D6}', NULL, '{nowIso}', {customerId}, NULL, 2, 10000, 0, 0, 0, 0, 10000, 10000, 0, 0, 5000, {shiftId}, '{nowIso}', 0, '{nowIso}', '{nowIso}', 1)");

                        // 10 lines per sale
                        for (int lineNo = 1; lineNo <= 10; lineNo++)
                        {
                            if (!firstItem) itemSb.Append(',');
                            firstItem = false;

                            int prodId = lineNo;
                            // Qty = 1000 (1 unit), UnitPrice = 1000 ($10), LineTotal = 1000 ($10)
                            itemSb.Append($"({lineIdCounter++}, {s}, {prodId}, 'Product {prodId}', 'SKU-{prodId:D4}', 'BAR-{prodId:D6}', 1000, 1000, 500, 0, 0, 0, 1000, {lineNo}, 0, '{nowIso}', '{nowIso}')");
                        }

                        // 1 Cash payment per sale
                        if (!firstPay) paySb.Append(',');
                        firstPay = false;
                        paySb.Append($"({s}, {s}, 1, 10000, 10000, 'Cash', '{nowIso}', 0, '{nowIso}', '{nowIso}', 1)");
                    }

                    saleSb.Append(';');
                    itemSb.Append(';');
                    paySb.Append(';');

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = saleSb.ToString();
                        await cmd.ExecuteNonQueryAsync();

                        cmd.CommandText = itemSb.ToString();
                        await cmd.ExecuteNonQueryAsync();

                        cmd.CommandText = paySb.ToString();
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await trans.CommitAsync();
            }

            // Test 1: Today Sales List query under 500ms
            using (var testCtx = new AppDbContext(options))
            {
                var saleRepo = new SaleRepository(testCtx);

                var todaySearchDto = new SaleSearchDto
                {
                    DateFrom = DateTime.Today,
                    DateTo = DateTime.Today.AddDays(1).AddTicks(-1),
                    PageNumber = 1,
                    PageSize = 50
                };

                // Warm-up query
                _ = await saleRepo.SearchSalesAsync(todaySearchDto);

                var sw = Stopwatch.StartNew();
                var result = await saleRepo.SearchSalesAsync(todaySearchDto);
                sw.Stop();

                result.TotalCount.Should().Be(5000);
                result.Items.Should().HaveCount(50);
                sw.ElapsedMilliseconds.Should().BeLessThan(500, $"Today sales list search took {sw.ElapsedMilliseconds}ms, expected under 500ms");
            }

            // Test 2: Shift summary query under 500ms
            using (var testCtx = new AppDbContext(options))
            {
                var shiftRepo = new ShiftRepository(testCtx);

                // Warm-up
                _ = await shiftRepo.GetShiftSummaryAsync(shiftId);

                var sw = Stopwatch.StartNew();
                var summary = await shiftRepo.GetShiftSummaryAsync(shiftId);
                sw.Stop();

                summary.SalesCount.Should().Be(5000);
                summary.NetSales.Should().Be(500000m); // 5000 * $100
                summary.ExpectedCash.Should().Be(500100m); // $100 opening + $500,000 cash sales
                sw.ElapsedMilliseconds.Should().BeLessThan(500, $"Shift summary took {sw.ElapsedMilliseconds}ms, expected under 500ms");
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
    public async Task BarcodeLookup_With20000Products_QueryCompletesUnder20ms()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"perf_barcode_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            using (var initCtx = new AppDbContext(options))
            {
                await initCtx.Database.MigrateAsync();

                var cat = new Category { Name = "Bulk Category", IsActive = true, CreatedAt = DateTime.UtcNow };
                var unit = new Unit { Name = "PCS", ShortName = "pcs", IsActive = true, CreatedAt = DateTime.UtcNow };
                initCtx.Categories.Add(cat);
                initCtx.Units.Add(unit);
                await initCtx.SaveChangesAsync();

                int catId = cat.Id;
                int unitId = unit.Id;

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
                const int batchSize = 1000;

                for (int b = 1; b <= totalProducts; b += batchSize)
                {
                    int bEnd = Math.Min(b + batchSize - 1, totalProducts);
                    var prodSb = new StringBuilder(20000);
                    prodSb.Append("INSERT INTO Products (Name, Sku, Barcode, CategoryId, UnitId, CostPrice, SalePrice, TaxPercent, ReorderLevel, TrackStock, IsActive, CreatedAt, IsDeleted) VALUES ");

                    for (int i = b; i <= bEnd; i++)
                    {
                        if (i > b) prodSb.Append(',');
                        prodSb.Append($"('Product {i:D6}', 'SKU-{i:D6}', 'BAR-{i:D8}', {catId}, {unitId}, 1000, 1500, 0, 5000, 1, 1, '{nowIso}', 0)");
                    }
                    prodSb.Append(';');

                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = trans;
                    cmd.CommandText = prodSb.ToString();
                    await cmd.ExecuteNonQueryAsync();
                }

                await trans.CommitAsync();
            }

            // Query test
            using (var testCtx = new AppDbContext(options))
            {
                var productRepo = new ProductRepository(testCtx);
                var lookupService = new ProductLookupService(productRepo);

                // Warm up
                _ = await lookupService.LookupProductAsync("BAR-00000001");

                // Target lookup
                var sw = Stopwatch.StartNew();
                var result = await lookupService.LookupProductAsync("BAR-00019999");
                sw.Stop();

                result.Found.Should().BeTrue();
                result.Product.Should().NotBeNull();
                result.Product!.Sku.Should().Be("SKU-019999");
                sw.ElapsedMilliseconds.Should().BeLessThan(20, $"Barcode lookup took {sw.ElapsedMilliseconds}ms, expected under 20ms");

                // Also test quick quantity parsing expression
                var parsedResult = await lookupService.LookupProductAsync("5*BAR-00015000");
                parsedResult.Found.Should().BeTrue();
                parsedResult.ParsedQuantity.Should().Be(5m);
                parsedResult.Product.Should().NotBeNull();
                parsedResult.Product!.Sku.Should().Be("SKU-015000");
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
