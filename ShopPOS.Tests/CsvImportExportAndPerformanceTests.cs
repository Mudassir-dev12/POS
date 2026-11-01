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

public class CsvImportExportAndPerformanceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly ProductRepository _productRepo;
    private readonly CsvProductService _csvService;
    private readonly Mock<IAuditLogService> _auditMock;
    private readonly PermissionService _permissionService;

    public CsvImportExportAndPerformanceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _context = new AppDbContext(options);
        _context.Database.Migrate();

        _productRepo = new ProductRepository(_context);
        _auditMock = new Mock<IAuditLogService>();
        _permissionService = new PermissionService();

        _csvService = new CsvProductService(_context, _productRepo, _auditMock.Object, _permissionService);

        // Seed initial reference category & unit
        _context.Categories.Add(new Category { Name = "General", IsActive = true });
        _context.Units.Add(new Unit { Name = "Pieces", ShortName = "pcs", AllowDecimal = false, IsActive = true });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ExportProductsToCsv_ShouldIncludeBomAndHeaderAndProducts()
    {
        // Arrange: Insert product
        var cat = await _context.Categories.FirstAsync();
        var unit = await _context.Units.FirstAsync();
        var prod = new Product
        {
            Name = "Export Test Item",
            Sku = "EXP-01",
            Barcode = "1112223334445",
            CategoryId = cat.Id,
            UnitId = unit.Id,
            CostPrice = 5.00m,
            SalePrice = 8.50m,
            IsActive = true
        };
        _context.Products.Add(prod);
        await _context.SaveChangesAsync();

        // Act
        var csvBytes = await _csvService.ExportProductsToCsvAsync(new ProductSearchQuery());

        // Assert: First 3 bytes should be UTF-8 BOM: 0xEF, 0xBB, 0xBF
        csvBytes.Length.Should().BeGreaterThan(3);
        csvBytes[0].Should().Be(0xEF);
        csvBytes[1].Should().Be(0xBB);
        csvBytes[2].Should().Be(0xBF);

        string csvContent = Encoding.UTF8.GetString(csvBytes);
        csvContent.Should().Contain("SKU,Barcode,Name,Category,Brand,Unit");
        csvContent.Should().Contain("EXP-01");
        csvContent.Should().Contain("Export Test Item");
    }

    [Fact]
    public async Task ValidateCsv_ShouldReportRowNumbersAndErrorsForInvalidRows()
    {
        // Arrange: CSV with 1 valid row, 1 missing name, 1 invalid cost
        string csv = @"SKU,Barcode,Name,Category,Brand,Unit,CostPrice,SalePrice
P001,1001,Valid Item,General,,pcs,5.00,10.00
P002,1002,,General,,pcs,5.00,10.00
P003,1003,Bad Price,General,,pcs,-10.00,10.00";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var report = await _csvService.ValidateCsvAsync(stream);

        // Assert
        report.Success.Should().BeTrue();
        report.Data!.TotalRows.Should().Be(3);
        report.Data.ValidRowCount.Should().Be(1);
        report.Data.ErrorRowCount.Should().Be(2);

        var errs = report.Data.Errors;
        errs.Should().Contain(e => e.RowNumber == 3 && e.Error.Contains("Name is required"));
        errs.Should().Contain(e => e.RowNumber == 4 && e.Error.Contains("Invalid Cost Price"));
    }

    [Fact]
    public async Task ImportCsv_ShouldAutoCreateMissingCategoriesAndUnitsAndRespectUpdateMode()
    {
        // Arrange: Initial product in database
        var cat = await _context.Categories.FirstAsync();
        var unit = await _context.Units.FirstAsync();
        var existing = new Product
        {
            Name = "Old Product Name",
            Sku = "MATCH-01",
            Barcode = "999888777",
            CategoryId = cat.Id,
            UnitId = unit.Id,
            CostPrice = 1.00m,
            SalePrice = 2.00m,
            IsActive = true
        };
        _context.Products.Add(existing);
        await _context.SaveChangesAsync();

        // CSV has:
        // 1) Matching SKU with updated price and NEW Category ("Organic Foods") and NEW Brand ("Nature Best")
        // 2) New product with no SKU (auto generate)
        string csv = @"SKU,Barcode,Name,Category,Brand,Unit,CostPrice,SalePrice
MATCH-01,999888777,Updated Product Name,Organic Foods,Nature Best,kg,1.50,3.00
,888111222,Fresh Lemons,Organic Foods,Nature Best,kg,0.80,1.50";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var options = new CsvImportOptions
        {
            UpdateExisting = true,
            UserId = 1,
            UserRole = RoleDefaults.Admin
        };

        // Act
        var res = await _csvService.ImportCsvAsync(stream, options);

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.ProcessedCount.Should().Be(2);
        res.Data.UpdatedCount.Should().Be(1);
        res.Data.InsertedCount.Should().Be(1);

        // Verify Auto-created category and brand
        var newCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Organic Foods");
        newCat.Should().NotBeNull();

        var newBrand = await _context.Brands.FirstOrDefaultAsync(b => b.Name == "Nature Best");
        newBrand.Should().NotBeNull();

        // Verify Existing product updated
        var updated = await _context.Products.FirstAsync(p => p.Sku == "MATCH-01");
        updated.Name.Should().Be("Updated Product Name");
        updated.SalePrice.Should().Be(3.00m);

        // Verify Price history recorded for updated product
        var history = await _context.ProductPriceHistories.FirstOrDefaultAsync(h => h.ProductId == updated.Id);
        history.Should().NotBeNull();
        history!.OldSalePrice.Should().Be(2.00m);
        history.NewSalePrice.Should().Be(3.00m);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task Performance_With20000Products_QueryShouldCompleteInUnder1Second()
    {
        // Arrange: Generate 20,000 products in database
        var cat = await _context.Categories.FirstAsync();
        var unit = await _context.Units.FirstAsync();

        const int totalToInsert = 20000;
        const int batchSize = 2000;

        for (int b = 0; b < totalToInsert; b += batchSize)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            var batch = new List<Product>(batchSize);
            for (int i = 0; i < batchSize; i++)
            {
                int num = b + i + 1;
                batch.Add(new Product
                {
                    Name = $"Bulk Performance Product {num:D6}",
                    Sku = $"BULK-{num:D6}",
                    Barcode = $"777{num:D10}",
                    CategoryId = cat.Id,
                    UnitId = unit.Id,
                    CostPrice = 5.00m,
                    SalePrice = 9.99m,
                    IsActive = true
                });
            }
            await _context.Products.AddRangeAsync(batch);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        // Verify count
        int count = await _context.Products.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(20000);

        // Act: Measure query time for paged search (Page size 50)
        var stopwatch = Stopwatch.StartNew();

        var query = new ProductSearchQuery
        {
            SearchTerm = "015500", // Search in 20,000 records
            PageNumber = 1,
            PageSize = 50
        };

        var pagedResult = await _productRepo.GetPagedAsync(query);
        stopwatch.Stop();

        // Assert: Under 1000 milliseconds (1 second)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Opening product list with 20,000 products must take under 1 second");
        pagedResult.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateCsv_WithSpacedHeadersLikeProductName_ShouldSucceedWithoutErrors()
    {
        // Arrange: Headers with spaces and aliases as commonly seen in user spreadsheets
        string csv = @"SKU,Barcode,Product Name,Category,Brand,Unit,Cost Price,Sale Price,Current Stock
P-SPACE-01,999888777,Spaced Header Product,Beverages,BrandX,pcs,12.50,18.00,45";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await _csvService.ValidateCsvAsync(stream);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.TotalRows.Should().Be(1);
        result.Data.ValidRowCount.Should().Be(1);
        result.Data.ErrorRowCount.Should().Be(0);
        result.Data.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportCsv_ShouldUpdateExistingProducts_WhenUpdateExistingIsTrue()
    {
        // Arrange: Existing product in database
        var cat = await _context.Categories.FirstAsync();
        var unit = await _context.Units.FirstAsync();
        var existing = new Product
        {
            Name = "Original Product Name",
            Sku = "EXIST-001",
            Barcode = "1234567890123",
            CategoryId = cat.Id,
            UnitId = unit.Id,
            CostPrice = 10m,
            SalePrice = 20m,
            ReorderLevel = 5m,
            IsActive = true
        };
        _context.Products.Add(existing);
        await _context.SaveChangesAsync();

        // CSV data updating the existing product's name, prices, and stock
        string csv = @"SKU,Barcode,Product Name,Category,Brand,Unit,Cost Price,Sale Price,Current Stock
EXIST-001,1234567890123,Updated Product Name,General,,pcs,15.00,25.00,30";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var options = new CsvImportOptions
        {
            UpdateExisting = true,
            UserId = 1,
            UserRole = RoleDefaults.Admin
        };

        // Act
        var result = await _csvService.ImportCsvAsync(stream, options);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.UpdatedCount.Should().Be(1);
        result.Data.InsertedCount.Should().Be(0);
        result.Data.ErrorCount.Should().Be(0);

        // Verify entity was updated
        var updated = await _context.Products.FirstAsync(p => p.Sku == "EXIST-001");
        updated.Name.Should().Be("Updated Product Name");
        updated.CostPrice.Should().Be(15m);
        updated.SalePrice.Should().Be(25m);

        // Verify stock adjustment was recorded
        var stockSum = await _context.StockMovements
            .Where(sm => sm.ProductId == updated.Id)
            .SumAsync(sm => sm.Quantity);
        stockSum.Should().Be(30m);
    }

    [Fact]
    public async Task ExportAndImportExcel_ShouldWorkSeamlessly()
    {
        // Arrange
        var cat = await _context.Categories.FirstAsync();
        var unit = await _context.Units.FirstAsync();
        var p = new Product
        {
            Name = "Excel Test Product",
            Sku = "XL-001",
            Barcode = "9876543210123",
            CategoryId = cat.Id,
            UnitId = unit.Id,
            CostPrice = 7.50m,
            SalePrice = 14.00m,
            ReorderLevel = 10m,
            IsActive = true
        };
        _context.Products.Add(p);
        await _context.SaveChangesAsync();

        // Act: Export to Excel (.xlsx)
        var excelBytes = await _csvService.ExportProductsToExcelAsync(new ProductSearchQuery());
        excelBytes.Should().NotBeNull();
        excelBytes.Length.Should().BeGreaterThan(0);

        // Preview and validate the generated Excel bytes
        using var stream = new MemoryStream(excelBytes);
        var previewRes = await _csvService.PreviewCsvAsync(stream);
        previewRes.Success.Should().BeTrue();
        previewRes.Data!.TotalRowsFound.Should().BeGreaterThanOrEqualTo(1);

        stream.Position = 0;
        var valRes = await _csvService.ValidateCsvAsync(stream);
        valRes.Success.Should().BeTrue();
        valRes.Data!.ErrorRowCount.Should().Be(0);
    }

    [Fact]
    public async Task ValidateAndImportCsv_WithSemicolonDelimiter_ShouldAutoDetectAndImportSuccessfully()
    {
        // Arrange: Semicolon delimited file as generated by Excel on many Windows regional settings
        string csv = "SKU;Barcode;Product Name;Category;Brand;Unit;Cost Price;Sale Price;Current Stock\r\n" +
                     "SEMI-01;555111222;Semicolon Product 1;General;;pcs;4.50;8.99;25\r\n" +
                     "SEMI-02;555111333;Semicolon Product 2;General;;pcs;10.00;19.99;50\r\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act: Preview
        var previewRes = await _csvService.PreviewCsvAsync(stream);
        previewRes.Success.Should().BeTrue();
        previewRes.Data!.TotalRowsFound.Should().Be(2);
        previewRes.Data.FileTypeInfo.Should().Contain("';'");
        previewRes.Data.DetectedNameColumn.Should().Be("Product Name");

        // Act: Validate
        stream.Position = 0;
        var valRes = await _csvService.ValidateCsvAsync(stream);
        valRes.Success.Should().BeTrue();
        valRes.Data!.ValidRowCount.Should().Be(2);
        valRes.Data.ErrorRowCount.Should().Be(0);

        // Act: Import
        stream.Position = 0;
        var options = new CsvImportOptions { UpdateExisting = true, UserId = 1, UserRole = RoleDefaults.Admin };
        var importRes = await _csvService.ImportCsvAsync(stream, options);
        importRes.Success.Should().BeTrue();
        importRes.Data!.InsertedCount.Should().Be(2);

        var p1 = await _context.Products.FirstOrDefaultAsync(p => p.Sku == "SEMI-01");
        p1.Should().NotBeNull();
        p1!.Name.Should().Be("Semicolon Product 1");
        p1.SalePrice.Should().Be(8.99m);
    }

    [Fact]
    public async Task ImportCsv_MatchingExistingByNameEvenWhenSkuIsDifferent_ShouldUpdateProduct()
    {
        // Arrange: Existing product in database
        var cat = await _context.Categories.FirstAsync();
        var unit = await _context.Units.FirstAsync();
        var existing = new Product
        {
            Name = "Existing Beverage",
            Sku = "BEV-001",
            Barcode = "999000111",
            CategoryId = cat.Id,
            UnitId = unit.Id,
            CostPrice = 2.00m,
            SalePrice = 3.50m,
            IsActive = true
        };
        _context.Products.Add(existing);
        await _context.SaveChangesAsync();

        // CSV where SKU is different or blank, but Name matches existing product
        string csv = "SKU,Barcode,Product Name,Category,Brand,Unit,Cost Price,Sale Price\r\n" +
                     "NEW-SKU-99,,Existing Beverage,General,,pcs,2.50,4.00\r\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var options = new CsvImportOptions { UpdateExisting = true, UserId = 1, UserRole = RoleDefaults.Admin };

        // Act
        var res = await _csvService.ImportCsvAsync(stream, options);

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.UpdatedCount.Should().Be(1);
        res.Data.InsertedCount.Should().Be(0);

        var updated = await _context.Products.FirstOrDefaultAsync(p => p.Name == "Existing Beverage");
        updated.Should().NotBeNull();
        updated!.SalePrice.Should().Be(4.00m);
        updated.CostPrice.Should().Be(2.50m);
    }

    [Fact]
    public void GetSampleExcelTemplate_ShouldReturnValidBytes()
    {
        var bytes = _csvService.GetSampleExcelTemplate();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(0);

        // OpenXML magic bytes PK
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task ImportCsv_With10000Rows_ShouldImportSuccessfullyInBatches()
    {
        // Arrange: Generate 10,000 row CSV in memory
        var sb = new StringBuilder();
        sb.AppendLine("SKU,Barcode,Name,Category,Brand,Unit,CostPrice,SalePrice");
        for (int i = 1; i <= 10000; i++)
        {
            sb.AppendLine($"IMPORT-{i:D6},BARCODE-{i:D6},Imported Product {i},Bulk Category,Bulk Brand,pcs,2.50,4.99");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        var options = new CsvImportOptions
        {
            UpdateExisting = false,
            UserId = 1,
            UserRole = RoleDefaults.Admin
        };

        int progressUpdates = 0;
        var progress = new Progress<int>(_ => Interlocked.Increment(ref progressUpdates));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _csvService.ImportCsvAsync(stream, options, progress);
        stopwatch.Stop();

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.InsertedCount.Should().Be(10000);
        result.Data.ErrorCount.Should().Be(0);

        // Verify that the batch commit (500 items per batch) worked properly in SQLite
        int totalProducts = await _context.Products.CountAsync(p => p.Sku.StartsWith("IMPORT-"));
        totalProducts.Should().Be(10000);
    }
}

