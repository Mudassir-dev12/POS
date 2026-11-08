using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.DTOs.DataTools;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Data;
using ShopPOS.Data.Seeding;
using Xunit;

namespace ShopPOS.Tests;

public class DataToolsTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempBackupDir;
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _context;
    private readonly IDataToolsService _dataToolsService;

    public DataToolsTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"ShopPOS_DataToolsTest_{Guid.NewGuid():N}.db");
        _tempBackupDir = Path.Combine(Path.GetTempPath(), $"ShopPOS_BackupTest_{Guid.NewGuid():N}");

        Directory.CreateDirectory(_tempBackupDir);

        var services = new ServiceCollection();
        var dbOptions = new DatabaseOptions { DbPath = _tempDbPath };

        services.AddCoreServices();
        services.AddDataServices(dbOptions);

        _serviceProvider = services.BuildServiceProvider();

        _context = _serviceProvider.GetRequiredService<AppDbContext>();
        _context.Database.Migrate();

        var seeder = _serviceProvider.GetRequiredService<IDataSeeder>();
        seeder.SeedAsync().GetAwaiter().GetResult();

        _dataToolsService = _serviceProvider.GetRequiredService<IDataToolsService>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _serviceProvider.Dispose();

        try
        {
            if (File.Exists(_tempDbPath))
            {
                File.Delete(_tempDbPath);
            }
            if (Directory.Exists(_tempBackupDir))
            {
                Directory.Delete(_tempBackupDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task GetCountsAsync_ShouldReturnAccurateLiveCounts()
    {
        var counts = await _dataToolsService.GetCountsAsync();
        counts.Should().NotBeNull();
        counts.SalesCount.Should().Be(0);
        counts.PurchasesCount.Should().Be(0);
        counts.UnitsCount.Should().BeGreaterThan(0);
        counts.CategoriesCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ClearTransactionsAsync_ClearsAllTransactions_ResetsSequences_AndCreatesBackup()
    {
        // 1. Arrange: Create a product and a sale transaction
        var category = await _context.Categories.FirstAsync();
        var unit = await _context.Units.FirstAsync();

        var product = new Product
        {
            Name = "Test Data Cleared Product",
            Sku = "TEST-CLR-001",
            Barcode = "2000000000017",
            CategoryId = category.Id,
            UnitId = unit.Id,
            CostPrice = 5000,
            SalePrice = 8000,
            IsActive = true
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var sale = new Sale
        {
            SaleNumber = "INV-000001",
            SaleDate = DateTime.Now,
            SubTotal = 8000,
            GrandTotal = 8000,
            PaidAmount = 8000,
            Status = SaleStatus.Completed
        };
        _context.Sales.Add(sale);
        await _context.SaveChangesAsync();

        var saleItem = new SaleItem
        {
            SaleId = sale.Id,
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = 8000,
            LineTotal = 8000
        };
        _context.SaleItems.Add(saleItem);

        var payment = new SalePayment
        {
            SaleId = sale.Id,
            Method = PaymentMethod.Cash,
            Amount = 8000,
            PaidAt = DateTime.Now
        };
        _context.SalePayments.Add(payment);

        var movement = new StockMovement
        {
            ProductId = product.Id,
            MovementType = StockMovementType.Sale,
            Quantity = -1,
            UnitCost = 5000,
            MovementDate = DateTime.Now,
            ReferenceType = "Sale",
            ReferenceId = sale.Id
        };
        _context.StockMovements.Add(movement);

        var expenseCat = await _context.ExpenseCategories.FirstAsync();
        var expense = new Expense
        {
            ExpenseNumber = "EXP-000001",
            ExpenseCategoryId = expenseCat.Id,
            Amount = 1500,
            Method = PaymentMethod.Cash,
            ExpenseDate = DateTime.Now,
            Notes = "Test Shop Expense"
        };
        _context.Expenses.Add(expense);

        _context.AuditLogs.Add(new AuditLog
        {
            Action = "TEST",
            EntityName = "Sale",
            Timestamp = DateTime.Now,
            Details = "Test log"
        });

        await _context.SaveChangesAsync();

        var countsBefore = await _dataToolsService.GetCountsAsync();
        countsBefore.SalesCount.Should().Be(1);
        countsBefore.ExpensesCount.Should().Be(1);
        countsBefore.AuditLogsCount.Should().BeGreaterThan(0);

        // 2. Act: Clear transactions
        var result = await _dataToolsService.ClearTransactionsAsync(_tempBackupDir);

        // 3. Assert
        result.Success.Should().BeTrue();
        result.BackupFilePath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.BackupFilePath!).Should().BeTrue();

        var countsAfter = await _dataToolsService.GetCountsAsync();
        countsAfter.SalesCount.Should().Be(0);
        countsAfter.SaleItemsCount.Should().Be(0);
        countsAfter.SalePaymentsCount.Should().Be(0);
        countsAfter.PurchasesCount.Should().Be(0);
        countsAfter.StockMovementsCount.Should().Be(0);
        countsAfter.ExpensesCount.Should().Be(0);
        countsAfter.AuditLogsCount.Should().Be(0);

        // Master data preserved
        countsAfter.ProductsCount.Should().BeGreaterThan(0);
        countsAfter.CategoriesCount.Should().BeGreaterThan(0);

        // Stock computed by stock ledger must be 0
        var ledger = _serviceProvider.GetRequiredService<IStockLedgerQuery>();
        var stock = await ledger.GetCurrentStockAsync(product.Id);
        stock.Should().Be(0);

        // Sequences must be reset to 0
        var seq = await _context.NumberSequences.FirstOrDefaultAsync(s => s.Key == "INV");
        seq.Should().NotBeNull();
        seq!.LastNumber.Should().Be(0);
    }

    [Fact]
    public async Task ClearEverythingAsync_ResetsShop_PreservesSettingsAndOwnerUser_AndReseedsDefaults()
    {
        // 1. Arrange: Add product
        var category = await _context.Categories.FirstAsync();
        var unit = await _context.Units.FirstAsync();
        _context.Products.Add(new Product
        {
            Name = "Shop Product To Wipe",
            Sku = "WIPE-001",
            Barcode = "2000000000024",
            CategoryId = category.Id,
            UnitId = unit.Id,
            CostPrice = 2000,
            SalePrice = 3000,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        // 2. Act: Clear everything
        var result = await _dataToolsService.ClearEverythingAsync(_tempBackupDir);

        // 3. Assert
        result.Success.Should().BeTrue();
        result.BackupFilePath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.BackupFilePath!).Should().BeTrue();

        var countsAfter = await _dataToolsService.GetCountsAsync();
        countsAfter.ProductsCount.Should().Be(0);
        countsAfter.SalesCount.Should().Be(0);
        countsAfter.PurchasesCount.Should().Be(0);
        countsAfter.ExpensesCount.Should().Be(0);

        // Reseeded defaults exist
        countsAfter.CategoriesCount.Should().BeGreaterThan(0);
        countsAfter.UnitsCount.Should().Be(5); // pcs, kg, litre, box, dozen
        countsAfter.ExpensesCount.Should().Be(0);

        var expenseCategories = await _context.ExpenseCategories.CountAsync();
        expenseCategories.Should().Be(8);

        // Local user preserved
        var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == 1);
        owner.Should().NotBeNull();

        // Settings preserved
        var settings = await _context.ShopSettings.FirstOrDefaultAsync();
        settings.Should().NotBeNull();
    }

    [Fact]
    public async Task ClearAuditLogOnlyAsync_ClearsAuditLogsOnly_PreservesAllOtherData()
    {
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "TEST_AUDIT",
            EntityName = "System",
            Timestamp = DateTime.Now,
            Details = "Test details"
        });
        await _context.SaveChangesAsync();

        var result = await _dataToolsService.ClearAuditLogOnlyAsync(_tempBackupDir);

        result.Success.Should().BeTrue();
        var counts = await _dataToolsService.GetCountsAsync();
        counts.AuditLogsCount.Should().Be(0);
        counts.CategoriesCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LoadDemoDataAsync_WhenTransactionsExist_RejectsWithExplanation()
    {
        // Arrange: insert a sale transaction
        _context.Sales.Add(new Sale
        {
            SaleNumber = "INV-999999",
            SaleDate = DateTime.Now,
            SubTotal = 1000,
            GrandTotal = 1000,
            PaidAmount = 1000,
            Status = SaleStatus.Completed
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _dataToolsService.LoadDemoDataAsync(_tempBackupDir);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Clear transactions");
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task LoadDemoDataAsync_WhenEmpty_PopulatesRealisticDemoShop_WithValidBarcodes()
    {
        // 1. Arrange: Clear everything to fresh state first
        await _dataToolsService.ClearEverythingAsync(_tempBackupDir);

        // 2. Act: Load demo data
        var result = await _dataToolsService.LoadDemoDataAsync(_tempBackupDir);

        // 3. Assert
        result.Success.Should().BeTrue();
        result.CategoriesCreated.Should().Be(6);
        result.BrandsCreated.Should().Be(6);
        result.SuppliersCreated.Should().Be(5);
        result.ProductsCreated.Should().BeGreaterThanOrEqualTo(50);
        result.PurchasesCreated.Should().Be(8);
        result.SalesCreated.Should().Be(40);
        result.ExpensesCreated.Should().Be(25);

        // Sample barcodes verification
        result.SampleBarcodes.Should().HaveCount(10);
        foreach (var sample in result.SampleBarcodes)
        {
            sample.Barcode.Should().HaveLength(13);
            sample.Barcode.Should().StartWith("2");
            sample.ProductName.Should().NotBeNullOrWhiteSpace();
            sample.SalePrice.Should().BeGreaterThan(0);
        }

        // Verify all demo barcodes start with 200..299 private in-store prefix
        var products = await _context.Products.ToListAsync();
        foreach (var prod in products)
        {
            prod.Barcode.Should().HaveLength(13);
            prod.Barcode.Should().StartWith("2");
        }
    }
}
