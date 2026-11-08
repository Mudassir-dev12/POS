using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Data;
using ShopPOS.Data.Seeding;
using Xunit;

namespace ShopPOS.Tests;

[Trait("Category", "Slow")]
public class PerformanceAndMemoryTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly string _tempDbPath;

    public PerformanceAndMemoryTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "ShopPOS_PerfMem_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
        _tempDbPath = Path.Combine(_tempFolder, "perf.db");
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

    [Fact]
    public async Task FiveHundredSalesLoop_ExecutesCleanly_WithoutMemoryLeak()
    {
        var services = new ServiceCollection();
        var dbOptions = new DatabaseOptions { DbPath = _tempDbPath };

        services.AddCoreServices();
        services.AddDataServices(dbOptions);

        var serviceProvider = services.BuildServiceProvider();

        int productId;

        // 1. Initialize schema and master data
        using (var scope = serviceProvider.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ctx.Database.MigrateAsync();

            var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
            await seeder.SeedAsync();

            var cat = await ctx.Categories.FirstAsync();
            var unit = await ctx.Units.FirstAsync();

            var product = new Product
            {
                Name = "Stress Test Product",
                Sku = "SKU-STRESS",
                Barcode = "1234567890123",
                CategoryId = cat.Id,
                UnitId = unit.Id,
                CostPrice = 50m,
                SalePrice = 100m,
                TrackStock = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
            productId = product.Id;
        }

        // Force GC before starting measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long memBefore = GC.GetTotalMemory(forceFullCollection: true);

        var stopwatch = Stopwatch.StartNew();

        // 2. Execute 500 sales loop through scoped ISaleService
        const int salesCount = 500;
        for (int i = 1; i <= salesCount; i++)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var saleService = scope.ServiceProvider.GetRequiredService<ISaleService>();

                var dto = new CompleteSaleDto
                {
                    Items = new List<SaleLineItemDto>
                    {
                        new()
                        {
                            ProductId = productId,
                            Quantity = 1m,
                            UnitPrice = 100m,
                            TaxPercent = 0m
                        }
                    },
                    Payments = new List<SalePaymentDto>
                    {
                        new()
                        {
                            Method = PaymentMethod.Cash,
                            Amount = 100m,
                            TenderedAmount = 100m
                        }
                    }
                };

                var result = await saleService.CompleteSaleAsync(dto);
                Assert.True(result.Success, result.ErrorMessage);
            }
        }

        stopwatch.Stop();

        // 3. Force GC after run
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long memAfter = GC.GetTotalMemory(forceFullCollection: true);

        long memDeltaBytes = memAfter - memBefore;
        long memDeltaMb = memDeltaBytes / (1024 * 1024);

        // Verify total completed sales in DB
        using (var scope = serviceProvider.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            int totalSales = await ctx.Sales.CountAsync();
            Assert.Equal(salesCount, totalSales);
        }

        // Memory leak assertion: Bounded growth under 50 MB for 500 sales scopes and transactions
        Assert.True(memDeltaMb < 50, $"Memory growth exceeded allowable limit: {memDeltaMb} MB delta");
    }
}
