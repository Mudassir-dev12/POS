using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Data;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Repositories;
using System.Data;
using System.Diagnostics;
using System.Text;

namespace ShopPOS.Tests;

public class PurchasePerformanceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public PurchasePerformanceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _context = new AppDbContext(options);
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task PurchaseList_5000Purchases_50000Lines_QueryCompletesUnder500Milliseconds()
    {
        // 1. Seed master data
        var supplier = new Supplier { Name = "Bulk Supplier Co", Phone = "999-000", IsActive = true };
        _context.Suppliers.Add(supplier);

        var unit = new Unit { Name = "Pieces", ShortName = "pcs", IsActive = true };
        var category = new Category { Name = "General", IsActive = true };
        _context.Units.Add(unit);
        _context.Categories.Add(category);

        var products = Enumerable.Range(1, 10).Select(i => new Product
        {
            Name = $"Product {i}",
            Sku = $"SKU-{i:D4}",
            Barcode = $"BAR-{i:D6}",
            CostPrice = 10m * i,
            SalePrice = 15m * i,
            Unit = unit,
            Category = category,
            IsActive = true
        }).ToList();

        _context.Products.AddRange(products);
        await _context.SaveChangesAsync();

        int supplierId = supplier.Id;
        var productIds = products.Select(p => p.Id).ToList();

        // 2. High-speed batched insert: 5,000 purchases with 10 lines each (50,000 lines) in a single SQLite transaction
        var dbConnection = _context.Database.GetDbConnection();
        if (dbConnection.State != ConnectionState.Open)
        {
            await dbConnection.OpenAsync();
        }

        using (var tx = dbConnection.BeginTransaction())
        {
            // Insert 5,000 Purchases in batches of 500
            for (int batch = 0; batch < 10; batch++)
            {
                var sb = new StringBuilder();
                sb.Append("INSERT INTO Purchases (Id, PurchaseNumber, SupplierId, PurchaseDate, Status, SubTotal, DiscountAmount, TaxAmount, FreightAmount, GrandTotal, PaidAmount, IsDeleted, CreatedAt, UpdatedAt) VALUES ");

                for (int i = 1; i <= 500; i++)
                {
                    int purchaseId = (batch * 500) + i;
                    string num = $"PUR-{purchaseId:D6}";
                    string date = DateTime.Today.AddDays(-i % 365).ToString("yyyy-MM-dd HH:mm:ss");

                    if (i > 1) sb.Append(',');
                    // Values: Id, PurchaseNumber, SupplierId, PurchaseDate, Status (Posted=2), SubTotal (10000 cents), Discount, Tax, Freight, GrandTotal, Paid, IsDeleted, CreatedAt, UpdatedAt
                    sb.Append($"({purchaseId}, '{num}', {supplierId}, '{date}', 2, 10000, 0, 0, 0, 10000, 10000, 0, '{date}', '{date}')");
                }
                sb.Append(';');

                using var cmd = dbConnection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sb.ToString();
                await cmd.ExecuteNonQueryAsync();
            }

            // Insert 50,000 PurchaseItems in batches of 1,000 lines (50 batches)
            int lineIdCounter = 1;
            for (int batch = 0; batch < 50; batch++)
            {
                var sb = new StringBuilder();
                sb.Append("INSERT INTO PurchaseItems (Id, PurchaseId, ProductId, ProductName, Sku, Quantity, UnitCost, DiscountPercent, TaxPercent, LineTotal, IsDeleted, CreatedAt, UpdatedAt) VALUES ");

                for (int i = 0; i < 1000; i++)
                {
                    int purchaseId = ((batch * 1000 + i) / 10) + 1;
                    int prodIndex = (batch * 1000 + i) % productIds.Count;
                    int prodId = productIds[prodIndex];
                    int lineId = lineIdCounter++;

                    if (i > 0) sb.Append(',');
                    // Values: Id, PurchaseId, ProductId, ProductName, Sku, Quantity (1000 thousandths = 1.0), UnitCost (1000 cents = 10.00), Discount, Tax, LineTotal (1000), IsDeleted, CreatedAt, UpdatedAt
                    sb.Append($"({lineId}, {purchaseId}, {prodId}, 'Product {prodIndex + 1}', 'SKU-{prodIndex + 1:D4}', 1000, 1000, 0, 0, 1000, 0, '2026-10-30 00:00:00', '2026-10-30 00:00:00')");
                }
                sb.Append(';');

                using var cmd = dbConnection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sb.ToString();
                await cmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }

        // 3. Test list query timing on 5,000 purchases / 50,000 items
        var purchaseRepo = new PurchaseRepository(_context);
        var query = new PurchaseSearchQuery
        {
            PageNumber = 1,
            PageSize = 50,
            Status = PurchaseStatus.Posted
        };

        // Warm up EF Core query pipeline
        await purchaseRepo.GetPurchasesAsync(new PurchaseSearchQuery { PageNumber = 1, PageSize = 1 });

        // Measure targeted benchmark
        var sw = Stopwatch.StartNew();
        var pagedResult = await purchaseRepo.GetPurchasesAsync(query);
        sw.Stop();

        // 4. Assert
        pagedResult.TotalCount.Should().Be(5000);
        pagedResult.Items.Should().HaveCount(50);
        sw.ElapsedMilliseconds.Should().BeLessThan(500, $"Querying a page of 50 from 5,000 purchases must complete in under 500 ms (Actual: {sw.ElapsedMilliseconds} ms)");
    }
}

