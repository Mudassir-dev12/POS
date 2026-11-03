using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;
using ShopPOS.Data;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class ProductSearchPerformanceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly ProductSearchService _service;

    public ProductSearchPerformanceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"shoppos_perf_{Guid.NewGuid():N}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath};Foreign Keys=True");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.Migrate();

        var stockLedgerQueryMock = new Mock<IStockLedgerQuery>();
        stockLedgerQueryMock.Setup(q => q.GetStockBatchAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(new Dictionary<int, decimal>());

        _service = new ProductSearchService(_context, stockLedgerQueryMock.Object);

        Seed20000Products();
    }

    private void Seed20000Products()
    {
        var unit = new Unit { Name = "Unit", ShortName = "u", IsActive = true };
        var category = new Category { Name = "General", IsActive = true };
        _context.Units.Add(unit);
        _context.Categories.Add(category);
        _context.SaveChanges();

        using var tx = _context.Database.BeginTransaction();
        var list = new List<Product>(1000);
        for (int i = 1; i <= 20000; i++)
        {
            var p = new Product
            {
                Sku = $"SKU-{i:D6}",
                Barcode = $"BAR{i:D10}",
                Name = $"Product {i} Flavor Variant Super Quality",
                CostPrice = 100m,
                SalePrice = 150m,
                UnitId = unit.Id,
                CategoryId = category.Id,
                TrackStock = true,
                ReorderLevel = 10m,
                IsActive = true
            };
            p.SearchText = p.ComputeSearchText();
            list.Add(p);

            if (list.Count >= 1000)
            {
                _context.Products.AddRange(list);
                _context.SaveChanges();
                list.Clear();
            }
        }
        if (list.Count > 0)
        {
            _context.Products.AddRange(list);
            _context.SaveChanges();
        }
        tx.Commit();
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task SearchAsync_20000Products_ExecutesUnder50Ms()
    {
        // Warm-up query
        await _service.SearchProductsAsync("Product 1");

        // Timed search query across 20,000 products
        var sw = Stopwatch.StartNew();
        var results = await _service.SearchProductsAsync("Variant 1999");
        sw.Stop();

        Assert.NotEmpty(results);
        Assert.True(sw.ElapsedMilliseconds < 50,
            $"Expected search under 50 ms, but took {sw.ElapsedMilliseconds} ms");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch { }
    }
}
