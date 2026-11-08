using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.DTOs.DataTools;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.Core.Validators;
using ShopPOS.Data;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Repositories;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class StockAdjustmentConcurrencyAndLifetimeTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDbContext _context;
    private readonly TransactionRunner _transactionRunner;
    private readonly ProductRepository _productRepo;
    private readonly StockAdjustmentRepository _adjustmentRepo;
    private readonly StockLedgerQuery _stockLedgerQuery;
    private readonly ShopSettingRepository _settingRepo;
    private readonly NumberSequenceService _sequenceService;
    private readonly StockAdjustmentService _adjustmentService;
    private readonly ProductService _productService;
    private readonly Mock<IAuditLogService> _auditMock;

    private Product _testProduct = null!;
    private User _testUser = null!;

    public StockAdjustmentConcurrencyAndLifetimeTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ShopPOS_Test_{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _context = new AppDbContext(options);
        _context.Database.Migrate();

        _transactionRunner = new TransactionRunner(_context);
        _productRepo = new ProductRepository(_context);
        _adjustmentRepo = new StockAdjustmentRepository(_context);
        _stockLedgerQuery = new StockLedgerQuery(_context);
        _settingRepo = new ShopSettingRepository(_context);
        _sequenceService = new NumberSequenceService(_context);
        _auditMock = new Mock<IAuditLogService>();

        _adjustmentService = new StockAdjustmentService(
            _adjustmentRepo,
            _productRepo,
            _stockLedgerQuery,
            _settingRepo,
            _sequenceService,
            _transactionRunner,
            _auditMock.Object,
            new CreateStockAdjustmentValidator(),
            new UpdateStockAdjustmentValidator());

        _productService = new ProductService(
            _productRepo,
            _settingRepo,
            _auditMock.Object,
            _transactionRunner,
            _adjustmentService);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var role = new Role { Name = RoleDefaults.Admin };
        _context.Roles.Add(role);

        _testUser = new User
        {
            Username = "adminuser",
            FullName = "Admin User",
            PasswordHash = "hash",
            Role = role,
            IsActive = true
        };
        _context.Users.Add(_testUser);

        var category = new Category { Name = "Test Category", IsActive = true };
        _context.Categories.Add(category);

        var unit = new Unit { Name = "Pieces", ShortName = "pcs", AllowDecimal = false, IsActive = true };
        _context.Units.Add(unit);

        var setting = new ShopSetting { ShopName = "Test Shop", AllowNegativeStock = false };
        _context.ShopSettings.Add(setting);
        _context.SaveChanges();

        _testProduct = new Product
        {
            Name = "Initial Product",
            Sku = "SKU-001",
            Barcode = "BAR-001",
            CategoryId = category.Id,
            UnitId = unit.Id,
            CostPrice = 100m,
            SalePrice = 150m,
            TrackStock = true,
            IsActive = true
        };
        _context.Products.Add(_testProduct);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task EditProductStock_TwiceInARow_SucceedsWithoutTrackingCollision()
    {
        var update1 = new UpdateProductDto
        {
            Id = _testProduct.Id,
            Name = _testProduct.Name,
            Sku = _testProduct.Sku,
            Barcode = _testProduct.Barcode,
            CategoryId = _testProduct.CategoryId,
            UnitId = _testProduct.UnitId,
            CostPrice = _testProduct.CostPrice,
            SalePrice = _testProduct.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 20m,
            StockAdjustmentNote = "First adjustment"
        };

        var result1 = await _productService.UpdateProductAsync(update1, _testUser.Id);
        result1.Success.Should().BeTrue(result1.ErrorMessage);

        var stockAfterFirst = await _stockLedgerQuery.GetCurrentStockAsync(_testProduct.Id);
        stockAfterFirst.Should().Be(20m);

        var update2 = new UpdateProductDto
        {
            Id = _testProduct.Id,
            Name = _testProduct.Name,
            Sku = _testProduct.Sku,
            Barcode = _testProduct.Barcode,
            CategoryId = _testProduct.CategoryId,
            UnitId = _testProduct.UnitId,
            CostPrice = _testProduct.CostPrice,
            SalePrice = _testProduct.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 35m,
            StockAdjustmentNote = "Second adjustment"
        };

        var result2 = await _productService.UpdateProductAsync(update2, _testUser.Id);
        result2.Success.Should().BeTrue(result2.ErrorMessage);

        var stockAfterSecond = await _stockLedgerQuery.GetCurrentStockAsync(_testProduct.Id);
        stockAfterSecond.Should().Be(35m);
    }

    [Fact]
    public async Task EditStock_AfterClearTransactionsResetsSequences_SucceedsWithoutKeyCollision()
    {
        // 1. Initial adjustment to populate stock adjustment id=1
        var update1 = new UpdateProductDto
        {
            Id = _testProduct.Id,
            Name = _testProduct.Name,
            Sku = _testProduct.Sku,
            Barcode = _testProduct.Barcode,
            CategoryId = _testProduct.CategoryId,
            UnitId = _testProduct.UnitId,
            CostPrice = _testProduct.CostPrice,
            SalePrice = _testProduct.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 10m,
            StockAdjustmentNote = "Pre-clear adjustment"
        };
        var res1 = await _productService.UpdateProductAsync(update1, _testUser.Id);
        res1.Success.Should().BeTrue();

        // 2. Clear transactions through DataToolsService while using the same app context
        var dataTools = new DataToolsService(
            _context,
            new Mock<IBarcodeService>().Object,
            new Mock<ISaleService>().Object,
            new Mock<ISaleReturnService>().Object,
            new Mock<IPurchaseService>().Object,
            new Mock<ISupplierPaymentService>().Object,
            new Mock<IPurchaseReturnService>().Object,
            _stockLedgerQuery);

        var clearResult = await dataTools.ClearTransactionsAsync();
        clearResult.Success.Should().BeTrue();

        // 3. Edit stock again. Sequence ADJ restarts at 1, Id will be 1.
        // Because ChangeTracker was cleared, no tracking collision occurs!
        var update2 = new UpdateProductDto
        {
            Id = _testProduct.Id,
            Name = _testProduct.Name,
            Sku = _testProduct.Sku,
            Barcode = _testProduct.Barcode,
            CategoryId = _testProduct.CategoryId,
            UnitId = _testProduct.UnitId,
            CostPrice = _testProduct.CostPrice,
            SalePrice = _testProduct.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 15m,
            StockAdjustmentNote = "Post-clear adjustment"
        };
        var res2 = await _productService.UpdateProductAsync(update2, _testUser.Id);
        res2.Success.Should().BeTrue($"Failed after clear transactions: {res2.ErrorMessage}");

        var finalStock = await _stockLedgerQuery.GetCurrentStockAsync(_testProduct.Id);
        finalStock.Should().Be(15m);
    }

    [Fact]
    public async Task EditStock_AfterFailedValidationOrSaveFailure_RetriesSuccessfully()
    {
        // 1. Attempt update with missing required note when stock changes
        var invalidDto = new UpdateProductDto
        {
            Id = _testProduct.Id,
            Name = _testProduct.Name,
            Sku = _testProduct.Sku,
            Barcode = _testProduct.Barcode,
            CategoryId = _testProduct.CategoryId,
            UnitId = _testProduct.UnitId,
            CostPrice = _testProduct.CostPrice,
            SalePrice = _testProduct.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 20m,
            StockAdjustmentNote = "" // Missing note!
        };
        var failResult1 = await _productService.UpdateProductAsync(invalidDto, _testUser.Id);
        failResult1.Success.Should().BeFalse();

        // 2. Attempt update with negative stock
        var negativeStockDto = new UpdateProductDto
        {
            Id = _testProduct.Id,
            Name = _testProduct.Name,
            Sku = _testProduct.Sku,
            Barcode = _testProduct.Barcode,
            CategoryId = _testProduct.CategoryId,
            UnitId = _testProduct.UnitId,
            CostPrice = _testProduct.CostPrice,
            SalePrice = _testProduct.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = -10m,
            StockAdjustmentNote = "Negative stock"
        };
        var failResult2 = await _productService.UpdateProductAsync(negativeStockDto, _testUser.Id);
        failResult2.Success.Should().BeFalse();

        // 3. Retry with valid data - must succeed with clean tracker
        var validDto = new UpdateProductDto
        {
            Id = _testProduct.Id,
            Name = _testProduct.Name,
            Sku = _testProduct.Sku,
            Barcode = _testProduct.Barcode,
            CategoryId = _testProduct.CategoryId,
            UnitId = _testProduct.UnitId,
            CostPrice = _testProduct.CostPrice,
            SalePrice = _testProduct.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 25m,
            StockAdjustmentNote = "Valid recovery adjustment"
        };
        var retryResult = await _productService.UpdateProductAsync(validDto, _testUser.Id);
        retryResult.Success.Should().BeTrue($"Retry failed: {retryResult.ErrorMessage}");

        var stock = await _stockLedgerQuery.GetCurrentStockAsync(_testProduct.Id);
        stock.Should().Be(25m);
    }

    [Fact]
    public async Task TwoQuickSavesInARow_BothSucceedWithoutCollision()
    {
        var save1 = new UpdateProductDto
        {
            Id = _testProduct.Id,
            Name = _testProduct.Name,
            Sku = _testProduct.Sku,
            Barcode = _testProduct.Barcode,
            CategoryId = _testProduct.CategoryId,
            UnitId = _testProduct.UnitId,
            CostPrice = _testProduct.CostPrice,
            SalePrice = _testProduct.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 50m,
            StockAdjustmentNote = "Quick save 1"
        };

        var save2 = new UpdateProductDto
        {
            Id = _testProduct.Id,
            Name = _testProduct.Name,
            Sku = _testProduct.Sku,
            Barcode = _testProduct.Barcode,
            CategoryId = _testProduct.CategoryId,
            UnitId = _testProduct.UnitId,
            CostPrice = _testProduct.CostPrice,
            SalePrice = _testProduct.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 75m,
            StockAdjustmentNote = "Quick save 2"
        };

        var res1 = await _productService.UpdateProductAsync(save1, _testUser.Id);
        var res2 = await _productService.UpdateProductAsync(save2, _testUser.Id);

        res1.Success.Should().BeTrue(res1.ErrorMessage);
        res2.Success.Should().BeTrue(res2.ErrorMessage);

        var finalStock = await _stockLedgerQuery.GetCurrentStockAsync(_testProduct.Id);
        finalStock.Should().Be(75m);
    }

    [Fact]
    public async Task StockAdjustment_ThroughAdjustmentsScreen_AfterProductDialogAdjustment_Succeeds()
    {
        // 1. Adjustment through Product Dialog
        var dialogUpdate = new UpdateProductDto
        {
            Id = _testProduct.Id,
            Name = _testProduct.Name,
            Sku = _testProduct.Sku,
            Barcode = _testProduct.Barcode,
            CategoryId = _testProduct.CategoryId,
            UnitId = _testProduct.UnitId,
            CostPrice = _testProduct.CostPrice,
            SalePrice = _testProduct.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 30m,
            StockAdjustmentNote = "Product dialog adjustment"
        };
        var resDialog = await _productService.UpdateProductAsync(dialogUpdate, _testUser.Id);
        resDialog.Success.Should().BeTrue(resDialog.ErrorMessage);

        // 2. Adjustment through Adjustments screen
        var resAdjustmentScreen = await _adjustmentService.CreateAndPostSingleAdjustmentAsync(
            _testProduct.Id,
            45m,
            "Adjustments screen physical count",
            StockAdjustmentReason.Found,
            _testUser.Id);

        resAdjustmentScreen.Success.Should().BeTrue(resAdjustmentScreen.ErrorMessage);

        var finalStock = await _stockLedgerQuery.GetCurrentStockAsync(_testProduct.Id);
        finalStock.Should().Be(45m);
    }

    [Fact]
    public void DbContext_IsRegisteredAsScoped_AndNotSingleton()
    {
        var services = new ServiceCollection();
        var tempDb = Path.Combine(Path.GetTempPath(), $"ShopPOS_DI_{Guid.NewGuid():N}.db");
        services.AddDataServices(new DatabaseOptions
        {
            DbPath = tempDb
        });

        var contextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AppDbContext));
        contextDescriptor.Should().NotBeNull("AppDbContext should be registered in DI");
        contextDescriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped, "AppDbContext must be Scoped, never Singleton");

        var factoryDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDbContextFactory<AppDbContext>));
        factoryDescriptor.Should().NotBeNull("IDbContextFactory<AppDbContext> must be registered in DI");
    }
}
