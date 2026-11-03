using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Data;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class ProductSearchServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IStockLedgerQuery> _stockLedgerQueryMock;
    private readonly ProductSearchService _service;

    public ProductSearchServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        _stockLedgerQueryMock = new Mock<IStockLedgerQuery>();
        _stockLedgerQueryMock.Setup(q => q.GetStockBatchAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(new Dictionary<int, decimal>());

        _service = new ProductSearchService(_context, _stockLedgerQueryMock.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var unit = new Unit { Id = 1, Name = "Piece", ShortName = "pc", IsActive = true };
        var category = new Category { Id = 1, Name = "Beverages", IsActive = true };
        _context.Units.Add(unit);
        _context.Categories.Add(category);

        var p1 = new Product
        {
            Id = 1,
            Sku = "COLA-500",
            Barcode = "1234567890",
            Name = "Coca Cola 500ml Bottle",
            CostPrice = 50m,
            SalePrice = 80m,
            UnitId = 1,
            CategoryId = 1,
            TrackStock = true,
            ReorderLevel = 10m,
            IsActive = true
        };
        p1.SearchText = p1.ComputeSearchText();

        var p2 = new Product
        {
            Id = 2,
            Sku = "PEPSI-500",
            Barcode = "2345678901",
            Name = "Pepsi Diet Cola 500ml",
            CostPrice = 45m,
            SalePrice = 75m,
            UnitId = 1,
            CategoryId = 1,
            TrackStock = true,
            ReorderLevel = 5m,
            IsActive = true
        };
        p2.SearchText = p2.ComputeSearchText();

        var p3 = new Product
        {
            Id = 3,
            Sku = "SPRITE-1000",
            Barcode = "3456789012",
            Name = "Sprite Lemon 1L",
            CostPrice = 60m,
            SalePrice = 100m,
            UnitId = 1,
            CategoryId = 1,
            TrackStock = true,
            ReorderLevel = 5m,
            IsActive = true
        };
        p3.SearchText = p3.ComputeSearchText();

        var p4 = new Product
        {
            Id = 4,
            Sku = "INACTIVE-COLA",
            Barcode = "9999999999",
            Name = "Inactive Cola 250ml",
            CostPrice = 30m,
            SalePrice = 50m,
            UnitId = 1,
            CategoryId = 1,
            TrackStock = true,
            IsActive = false
        };
        p4.SearchText = p4.ComputeSearchText();

        _context.Products.AddRange(p1, p2, p3, p4);
        _context.SaveChanges();
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitivePartialMatch_FindsMatches()
    {
        var results = await _service.SearchProductsAsync("cola");
        Assert.Equal(2, results.Count); // p1 and p2, excluding inactive p4
        Assert.Contains(results, r => r.ProductId == 1);
        Assert.Contains(results, r => r.ProductId == 2);
    }

    [Fact]
    public async Task SearchAsync_MultiWordAnywhere_MatchesOutOfOrder()
    {
        var results = await _service.SearchProductsAsync("500 cola");
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name.Contains("Coca Cola"));
        Assert.Contains(results, r => r.Name.Contains("Pepsi Diet Cola"));
    }

    [Fact]
    public async Task SearchAsync_ByBarcode_ReturnsExactMatch()
    {
        var results = await _service.SearchProductsAsync("1234567890");
        Assert.Single(results);
        Assert.Equal(1, results[0].ProductId);
    }

    [Fact]
    public async Task SearchAsync_BySku_ReturnsExactMatch()
    {
        var results = await _service.SearchProductsAsync("SPRITE-1000");
        Assert.Single(results);
        Assert.Equal(3, results[0].ProductId);
    }

    [Fact]
    public void ParseQuantityPrefix_ParsesQuantityCorrectly()
    {
        var (qty, term) = _service.ParseQuantityPrefix("5*cola");
        Assert.Equal(5m, qty);
        Assert.Equal("cola", term);
    }

    [Fact]
    public void ParseQuantityPrefix_DecimalQuantityPrefix_ParsesDecimalQuantity()
    {
        var (qty, term) = _service.ParseQuantityPrefix("2.5*sprite");
        Assert.Equal(2.5m, qty);
        Assert.Equal("sprite", term);
    }

    [Fact]
    public async Task SearchAsync_InactiveProductsExcluded_NeverReturned()
    {
        var results = await _service.SearchProductsAsync("Inactive");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_MaxTwentyResultsReturned()
    {
        // Add 25 products
        for (int i = 10; i < 35; i++)
        {
            var p = new Product
            {
                Id = i,
                Sku = $"TEST-{i}",
                Name = $"Bulk Test Product {i}",
                UnitId = 1,
                CategoryId = 1,
                IsActive = true
            };
            p.SearchText = p.ComputeSearchText();
            _context.Products.Add(p);
        }
        _context.SaveChanges();

        var results = await _service.SearchProductsAsync("Bulk Test");
        Assert.True(results.Count <= 20);
    }

    [Fact]
    public async Task SearchAsync_StockStatus_DeterminedFromStockLedger()
    {
        _stockLedgerQueryMock.Setup(q => q.GetStockBatchAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(new Dictionary<int, decimal>
            {
                { 1, 5m },   // Low stock (<= 10)
                { 2, 0m },   // Out of stock (<= 0)
                { 3, 20m }   // OK (> 5)
            });

        var results = await _service.SearchProductsAsync("Bottle");
        Assert.Single(results);
        Assert.Equal(StockStatus.Low, results[0].StockStatus);
        Assert.Equal("Low stock", results[0].StockStatusDisplay);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
