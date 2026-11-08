using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs.Reports;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Data;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Repositories;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

[Collection("PerformanceTests")]
[Trait("Category", "Slow")]
public class ReportPerformanceTests
{
    [Fact]
    public async Task ReportQueries_AndDashboard_ExecuteUnderTimeThresholds_WithLargeDataset()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"perf_reports_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            using (var ctx = new AppDbContext(options))
            {
                await ctx.Database.MigrateAsync();

                var role = new Role { Name = "Owner" };
                ctx.Roles.Add(role);
                var user = new User { Username = "owner", FullName = "Owner", Role = role, IsActive = true };
                ctx.Users.Add(user);

                var setting = new ShopSetting { ShopName = "Perf Shop", CurrencySymbol = "Rs" };
                ctx.ShopSettings.Add(setting);

                var cat = new Category { Name = "Category 1", IsActive = true };
                var brand = new Brand { Name = "Brand 1", IsActive = true };
                var unit = new Unit { Name = "Pcs", ShortName = "pcs", IsActive = true };
                ctx.Categories.Add(cat);
                ctx.Brands.Add(brand);
                ctx.Units.Add(unit);
                await ctx.SaveChangesAsync();

                // Products
                var products = new List<Product>();
                for (int i = 1; i <= 20; i++)
                {
                    products.Add(new Product
                    {
                        Name = $"Perf Product {i}",
                        Sku = $"SKU-{i:000}",
                        CategoryId = cat.Id,
                        BrandId = brand.Id,
                        UnitId = unit.Id,
                        CostPrice = 50m * i,
                        SalePrice = 80m * i,
                        IsActive = true
                    });
                }
                ctx.Products.AddRange(products);
                await ctx.SaveChangesAsync();

                // Batch insert 2,000 sales with 4,000 items and 2,000 payments using raw SQL for fast setup
                var conn = (SqliteConnection)ctx.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                using (var trans = conn.BeginTransaction())
                {
                    const int totalSales = 2000;
                    const int batchSize = 500;
                    int lineIdCounter = 1;

                    for (int b = 1; b <= totalSales; b += batchSize)
                    {
                        int bEnd = Math.Min(b + batchSize - 1, totalSales);

                        var saleSb = new StringBuilder();
                        saleSb.Append("INSERT INTO Sales (Id, SaleNumber, HoldNumber, SaleDate, PriceLevelId, Status, SubTotal, LineDiscountTotal, BillDiscountAmount, TaxAmount, RoundOff, GrandTotal, PaidAmount, ChangeAmount, DueAmount, TotalCost, CompletedAt, IsDeleted, CreatedAt, UpdatedAt, CreatedByUserId) VALUES ");

                        var itemSb = new StringBuilder();
                        itemSb.Append("INSERT INTO SaleItems (Id, SaleId, ProductId, ProductName, Sku, Barcode, Quantity, UnitPrice, CostPriceSnapshot, DiscountPercent, DiscountAmount, TaxPercent, LineTotal, LineNo, IsDeleted, CreatedAt, UpdatedAt) VALUES ");

                        var paySb = new StringBuilder();
                        paySb.Append("INSERT INTO SalePayments (Id, SaleId, Method, Amount, TenderedAmount, Reference, PaidAt, IsDeleted, CreatedAt, UpdatedAt, CreatedByUserId) VALUES ");

                        bool firstSale = true;
                        bool firstItem = true;
                        bool firstPay = true;

                        for (int s = b; s <= bEnd; s++)
                        {
                            var saleDate = DateTime.Today.AddDays(-15).AddHours(s % 360).ToString("yyyy-MM-dd HH:mm:ss");

                            if (!firstSale) saleSb.Append(',');
                            firstSale = false;
                            saleSb.Append($"({s}, 'INV-PRF-{s:D6}', NULL, '{saleDate}', NULL, 2, 160, 0, 0, 0, 0, 160, 160, 0, 0, 100, '{saleDate}', 0, '{saleDate}', '{saleDate}', 1)");

                            for (int lineNo = 1; lineNo <= 2; lineNo++)
                            {
                                if (!firstItem) itemSb.Append(',');
                                firstItem = false;
                                int prodId = products[lineNo - 1].Id;
                                itemSb.Append($"({lineIdCounter++}, {s}, {prodId}, 'Perf Product {lineNo}', 'SKU-{lineNo:000}', 'BAR-{lineNo}', 1, 80, 50, 0, 0, 0, 80, {lineNo}, 0, '{saleDate}', '{saleDate}')");
                            }

                            if (!firstPay) paySb.Append(',');
                            firstPay = false;
                            paySb.Append($"({s}, {s}, 0, 160, 160, 'Cash', '{saleDate}', 0, '{saleDate}', '{saleDate}', 1)");
                        }

                        saleSb.Append(';');
                        itemSb.Append(';');
                        paySb.Append(';');

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = trans;
                            cmd.CommandText = saleSb.ToString();
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = itemSb.ToString();
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = paySb.ToString();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    trans.Commit();
                }
            }

            // Test execution performance
            using (var testCtx = new AppDbContext(options))
            {
                var settingRepo = new ShopSettingRepository(testCtx);
                var barcodeService = new BarcodeService(testCtx);
                var supplierRepo = new SupplierLedgerRepository(testCtx);
                var reportService = new ReportService(testCtx, settingRepo, barcodeService, supplierRepo);

                var range = DateRangeDto.FromPreset(ReportDatePreset.ThisMonth);

                // 1. Daily Sales Summary
                var sw = Stopwatch.StartNew();
                var daily = await reportService.GetDailySalesSummaryAsync(range);
                sw.Stop();
                sw.ElapsedMilliseconds.Should().BeLessThan(2000, "Daily sales summary over large dataset must complete under 2 seconds");
                daily.TotalSalesCount.Should().BeGreaterThan(0);

                // 2. Profit and Loss
                sw.Restart();
                var pnl = await reportService.GetProfitAndLossAsync(range);
                sw.Stop();
                sw.ElapsedMilliseconds.Should().BeLessThan(2000, "Profit and Loss report must complete under 2 seconds");

                // 3. Sales By Product
                sw.Restart();
                var byProduct = await reportService.GetSalesByProductAsync(range, SalesGroupBy.Product, topN: 10);
                sw.Stop();
                sw.ElapsedMilliseconds.Should().BeLessThan(2000, "Sales by Product must complete under 2 seconds");
                byProduct.Rows.Should().NotBeEmpty();

                // 4. Dashboard Summary
                sw.Restart();
                var dashboard = await reportService.GetDashboardSummaryAsync();
                sw.Stop();
                sw.ElapsedMilliseconds.Should().BeLessThan(1000, "Dashboard summary must complete under 1 second");
                dashboard.Last30DaysTrend.Should().HaveCount(30);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { /* Ignore */ }
            }
        }
    }
}
