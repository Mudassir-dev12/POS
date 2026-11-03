using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Repositories;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Products;

namespace ShopPOS.Tests;

public class StockAdjustmentAndEditTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly ProductRepository _productRepo;
    private readonly CategoryRepository _categoryRepo;
    private readonly BrandRepository _brandRepo;
    private readonly UnitRepository _unitRepo;
    private readonly PriceLevelRepository _priceLevelRepo;
    private readonly Mock<IAuditLogService> _auditMock;
    private readonly Mock<IShopSettingRepository> _shopSettingMock;
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly BrandService _brandService;
    private readonly UnitService _unitService;
    private readonly Mock<IPriceLevelService> _priceLevelServiceMock;
    private readonly Mock<IDialogService> _dialogServiceMock;

    private Category _category = null!;
    private Unit _unit = null!;
    private User _adminUser = null!;

    public StockAdjustmentAndEditTests()
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
        _categoryRepo = new CategoryRepository(_context);
        _brandRepo = new BrandRepository(_context);
        _unitRepo = new UnitRepository(_context);
        _priceLevelRepo = new PriceLevelRepository(_context);

        _auditMock = new Mock<IAuditLogService>();
        _shopSettingMock = new Mock<IShopSettingRepository>();
        _shopSettingMock.Setup(s => s.GetCurrentSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShopSetting { DefaultTaxPercent = 5.0m, CurrencySymbol = "Rs" });


        _productService = new ProductService(_productRepo, _shopSettingMock.Object, _auditMock.Object);
        _categoryService = new CategoryService(_categoryRepo, _auditMock.Object);
        _brandService = new BrandService(_brandRepo, _auditMock.Object);
        _unitService = new UnitService(_unitRepo, _auditMock.Object);

        _priceLevelServiceMock = new Mock<IPriceLevelService>();
        _priceLevelServiceMock.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok<IReadOnlyList<PriceLevelDto>>(new List<PriceLevelDto>()));

        _dialogServiceMock = new Mock<IDialogService>();

        SeedTestData();
    }

    private void SeedTestData()
    {
        _category = new Category { Name = "General Category", IsActive = true };
        _context.Categories.Add(_category);

        _unit = new Unit { Name = "Box", ShortName = "bx", AllowDecimal = false, IsActive = true };
        _context.Units.Add(_unit);

        var adminRole = new Role { Name = RoleDefaults.Admin };
        _context.Roles.Add(adminRole);

        _adminUser = new User
        {
            Username = "adminuser",
            FullName = "Admin User",
            PasswordHash = "hash",
            Role = adminRole,
            IsActive = true
        };
        _context.Users.Add(_adminUser);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    private async Task<ProductDto> CreateTestProductWithOpeningStockAsync(decimal openingQty)
    {
        var createDto = new CreateProductDto
        {
            Name = "Inventory Test Product",
            Sku = "P-TEST-001",
            Barcode = "1234567890123",
            CategoryId = _category.Id,
            UnitId = _unit.Id,
            CostPrice = 20.00m,
            SalePrice = 35.00m,
            OpeningQuantity = openingQty,
            TrackStock = true
        };

        var res = await _productService.CreateProductAsync(createDto, _adminUser.Id, RoleDefaults.Admin);
        res.Success.Should().BeTrue();
        return res.Data!;
    }

    [Fact]
    public async Task RequirementA_ProductCreatedWithOpening10_WhenLoadedForEdit_ShowsCurrentStock10()
    {
        // 1. Arrange & Act: Create product with opening 10
        var created = await CreateTestProductWithOpeningStockAsync(10m);

        // 2. Load single product by Id (used by edit dialog)
        var loadRes = await _productService.GetProductByIdAsync(created.Id);

        // Assert
        loadRes.Success.Should().BeTrue();
        loadRes.Data!.CurrentStock.Should().Be(10m);

        // 3. Verify ProductDialogViewModel pre-fills both CurrentStock and AdjustedStock with 10m
        var vm = new ProductDialogViewModel(
            _productService,
            _categoryService,
            _brandService,
            _unitService,
            _priceLevelServiceMock.Object,
            _dialogServiceMock.Object,
            _adminUser.Id,
            RoleDefaults.Admin);

        await vm.InitializeAsync(created.Id);

        vm.IsEditMode.Should().BeTrue();
        vm.CurrentStock.Should().Be(10m);
        vm.AdjustedStock.Should().Be(10m);
    }

    [Fact]
    public async Task RequirementB_SavingEditWithStockUntouched_CreatesNoStockMovement()
    {
        // Arrange
        var created = await CreateTestProductWithOpeningStockAsync(10m);

        var initialMovements = await _context.StockMovements
            .Where(sm => sm.ProductId == created.Id)
            .ToListAsync();
        initialMovements.Should().HaveCount(1);
        initialMovements[0].MovementType.Should().Be(StockMovementType.Opening);

        // Act: Save edit with untouched stock (NewStockQuantity = 10m, reason is null)
        var updateDto = new UpdateProductDto
        {
            Id = created.Id,
            Name = "Inventory Test Product Renamed",
            Sku = created.Sku,
            CategoryId = created.CategoryId,
            UnitId = created.UnitId,
            CostPrice = created.CostPrice,
            SalePrice = created.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 10m,
            StockAdjustmentNote = null
        };

        var updateRes = await _productService.UpdateProductAsync(updateDto, _adminUser.Id, RoleDefaults.Admin);

        // Assert
        updateRes.Success.Should().BeTrue();
        updateRes.Data!.Name.Should().Be("Inventory Test Product Renamed");

        var afterMovements = await _context.StockMovements
            .Where(sm => sm.ProductId == created.Id)
            .ToListAsync();
        afterMovements.Should().HaveCount(1, "Untouched stock must not create any new StockMovement");

        var currentStock = await _productRepo.GetCurrentStockAsync(created.Id);
        currentStock.Should().Be(10m);
    }

    [Fact]
    public async Task RequirementC_AdjustingTo7WithReason_CreatesOneAdjustmentOfMinus3_AndLedgerSumIs7()
    {
        // Arrange
        var created = await CreateTestProductWithOpeningStockAsync(10m);

        // Act: Adjust stock to 7 with a reason
        var updateDto = new UpdateProductDto
        {
            Id = created.Id,
            Name = created.Name,
            Sku = created.Sku,
            CategoryId = created.CategoryId,
            UnitId = created.UnitId,
            CostPrice = created.CostPrice,
            SalePrice = created.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 7m,
            StockAdjustmentNote = "Physical stock count - 3 items damaged"
        };

        var updateRes = await _productService.UpdateProductAsync(updateDto, _adminUser.Id, RoleDefaults.Admin);

        // Assert
        updateRes.Success.Should().BeTrue();

        var movements = await _context.StockMovements
            .Where(sm => sm.ProductId == created.Id)
            .OrderBy(sm => sm.Id)
            .ToListAsync();

        movements.Should().HaveCount(2);
        movements[0].MovementType.Should().Be(StockMovementType.Opening);
        movements[0].Quantity.Should().Be(10m);

        movements[1].MovementType.Should().Be(StockMovementType.Adjustment);
        movements[1].Quantity.Should().Be(-3m);
        movements[1].Note.Should().Be("Physical stock count - 3 items damaged");

        var ledgerSum = await _productRepo.GetCurrentStockAsync(created.Id);
        ledgerSum.Should().Be(7m);

        // Verify AuditLog
        _auditMock.Verify(a => a.LogAsync(
            _adminUser.Id,
            "AdjustStock",
            "Product",
            created.Id,
            It.Is<string>(s => s.Contains("10") && s.Contains("7") && s.Contains("Physical stock count"))),
            Times.Once);
    }

    [Fact]
    public async Task RequirementD_AdjustingWithoutReason_IsRejected_AndCreatesNoMovement()
    {
        // Arrange
        var created = await CreateTestProductWithOpeningStockAsync(10m);

        // Act: Adjust stock to 7 without a reason
        var updateDto = new UpdateProductDto
        {
            Id = created.Id,
            Name = created.Name,
            Sku = created.Sku,
            CategoryId = created.CategoryId,
            UnitId = created.UnitId,
            CostPrice = created.CostPrice,
            SalePrice = created.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 7m,
            StockAdjustmentNote = "   " // Whitespace or empty
        };

        var updateRes = await _productService.UpdateProductAsync(updateDto, _adminUser.Id, RoleDefaults.Admin);

        // Assert
        updateRes.Success.Should().BeFalse();
        updateRes.ErrorMessage.Should().Contain("reason");

        var movements = await _context.StockMovements
            .Where(sm => sm.ProductId == created.Id)
            .ToListAsync();
        movements.Should().HaveCount(1, "Rejected adjustment must not create any movement");

        var ledgerSum = await _productRepo.GetCurrentStockAsync(created.Id);
        ledgerSum.Should().Be(10m);
    }

    [Fact]
    public async Task RequirementE_SingleUser_AdjustStock_SucceedsAndCreatesMovement()
    {
        // Arrange
        var created = await CreateTestProductWithOpeningStockAsync(10m);

        // Act: User adjusts stock
        var updateDto = new UpdateProductDto
        {
            Id = created.Id,
            Name = created.Name,
            Sku = created.Sku,
            CategoryId = created.CategoryId,
            UnitId = created.UnitId,
            CostPrice = created.CostPrice,
            SalePrice = created.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 7m,
            StockAdjustmentNote = "User adjusting stock"
        };

        var updateRes = await _productService.UpdateProductAsync(updateDto, 1, "Owner");

        // Assert
        updateRes.Success.Should().BeTrue();

        var movements = await _context.StockMovements
            .Where(sm => sm.ProductId == created.Id)
            .ToListAsync();
        movements.Should().HaveCount(2);

        var ledgerSum = await _productRepo.GetCurrentStockAsync(created.Id);
        ledgerSum.Should().Be(7m);
    }

    [Fact]
    public async Task RequirementF_SecondAdjustmentTo12_CreatesPlus5_AndLedgerSumIs12()
    {
        // Arrange
        var created = await CreateTestProductWithOpeningStockAsync(10m);

        // First adjustment: from 10 to 7
        var firstUpdate = new UpdateProductDto
        {
            Id = created.Id,
            Name = created.Name,
            Sku = created.Sku,
            CategoryId = created.CategoryId,
            UnitId = created.UnitId,
            CostPrice = created.CostPrice,
            SalePrice = created.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 7m,
            StockAdjustmentNote = "First adjustment to 7"
        };
        var firstRes = await _productService.UpdateProductAsync(firstUpdate, _adminUser.Id, RoleDefaults.Admin);
        firstRes.Success.Should().BeTrue();

        // Act: Second adjustment: from 7 to 12 as Manager
        var secondUpdate = new UpdateProductDto
        {
            Id = created.Id,
            Name = created.Name,
            Sku = created.Sku,
            CategoryId = created.CategoryId,
            UnitId = created.UnitId,
            CostPrice = created.CostPrice,
            SalePrice = created.SalePrice,
            TrackStock = true,
            IsActive = true,
            NewStockQuantity = 12m,
            StockAdjustmentNote = "Found extra box in warehouse"
        };
        var secondRes = await _productService.UpdateProductAsync(secondUpdate, _adminUser.Id, RoleDefaults.Manager);

        // Assert
        secondRes.Success.Should().BeTrue();

        var movements = await _context.StockMovements
            .Where(sm => sm.ProductId == created.Id)
            .OrderBy(sm => sm.Id)
            .ToListAsync();

        movements.Should().HaveCount(3);
        movements[0].Quantity.Should().Be(10m); // Opening
        movements[1].Quantity.Should().Be(-3m); // Adjustment 1
        movements[2].MovementType.Should().Be(StockMovementType.Adjustment);
        movements[2].Quantity.Should().Be(5m); // Adjustment 2: 12 - 7 = +5
        movements[2].Note.Should().Be("Found extra box in warehouse");

        var ledgerSum = await _productRepo.GetCurrentStockAsync(created.Id);
        ledgerSum.Should().Be(12m);
    }

    [Fact]
    public async Task ProductDialogViewModel_Validation_RequiresReasonWhenStockIsChanged()
    {
        // Arrange
        var created = await CreateTestProductWithOpeningStockAsync(10m);

        var vm = new ProductDialogViewModel(
            _productService,
            _categoryService,
            _brandService,
            _unitService,
            _priceLevelServiceMock.Object,
            _dialogServiceMock.Object,
            _adminUser.Id,
            RoleDefaults.Admin);

        await vm.InitializeAsync(created.Id);

        // Act: Change stock to 7 without entering a note
        vm.AdjustedStock = 7m;
        vm.StockAdjustmentNote = string.Empty;

        bool closed = false;
        vm.RequestClose += (success) => closed = true;

        await vm.SaveAsync();

        // Assert
        closed.Should().BeFalse();
        vm.StockAdjustmentError.Should().NotBeNullOrWhiteSpace();
        vm.StockAdjustmentError.Should().Contain("reason");
    }
}

