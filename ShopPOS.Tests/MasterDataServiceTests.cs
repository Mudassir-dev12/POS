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

public class MasterDataServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly ProductRepository _productRepo;
    private readonly CategoryRepository _categoryRepo;
    private readonly BrandRepository _brandRepo;
    private readonly UnitRepository _unitRepo;
    private readonly SupplierRepository _supplierRepo;
    private readonly PriceLevelRepository _priceLevelRepo;
    private readonly Mock<IAuditLogService> _auditMock;
    private readonly Mock<IShopSettingRepository> _shopSettingMock;

    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly BrandService _brandService;
    private readonly UnitService _unitService;
    private readonly SupplierService _supplierService;

    private Category _defaultCategory = null!;
    private Unit _defaultUnit = null!;
    private User _testUser = null!;

    public MasterDataServiceTests()
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
        _supplierRepo = new SupplierRepository(_context);
        _priceLevelRepo = new PriceLevelRepository(_context);

        _auditMock = new Mock<IAuditLogService>();
        _shopSettingMock = new Mock<IShopSettingRepository>();
        _shopSettingMock.Setup(s => s.GetCurrentSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShopSetting { DefaultTaxPercent = 5.0m, CurrencySymbol = "Rs" });


        _productService = new ProductService(_productRepo, _shopSettingMock.Object, _auditMock.Object);
        _categoryService = new CategoryService(_categoryRepo, _auditMock.Object);
        _brandService = new BrandService(_brandRepo, _auditMock.Object);
        _unitService = new UnitService(_unitRepo, _auditMock.Object);
        _supplierService = new SupplierService(_supplierRepo, _auditMock.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        _defaultCategory = new Category { Name = "General", IsActive = true };
        _context.Categories.Add(_defaultCategory);

        _defaultUnit = new Unit { Name = "Pieces", ShortName = "pcs", AllowDecimal = false, IsActive = true };
        _context.Units.Add(_defaultUnit);

        var adminRole = new Role { Name = RoleDefaults.Admin };
        _context.Roles.Add(adminRole);

        _testUser = new User
        {
            Username = "testadmin",
            FullName = "Test Admin",
            PasswordHash = "hash",
            Role = adminRole,
            IsActive = true
        };
        _context.Users.Add(_testUser);

        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateProduct_WithBlankSku_ShouldAutoGenerateSequentialSku()
    {
        // Arrange
        var dto = new CreateProductDto
        {
            Name = "Apple Juice",
            Sku = null, // Blank SKU
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 2.00m,
            SalePrice = 3.50m
        };

        // Act
        var result = await _productService.CreateProductAsync(dto, 1, RoleDefaults.Admin);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Sku.Should().Be("P000001");

        // Create second product with blank SKU
        var dto2 = new CreateProductDto
        {
            Name = "Orange Juice",
            Sku = "",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 2.50m,
            SalePrice = 4.00m
        };

        var result2 = await _productService.CreateProductAsync(dto2, 1, RoleDefaults.Admin);
        result2.Success.Should().BeTrue();
        result2.Data!.Sku.Should().Be("P000002");
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateBarcode_ShouldBlockAndIdentifyConflict()
    {
        // Arrange: Create first product with barcode "8901234567890"
        var firstDto = new CreateProductDto
        {
            Name = "Original Cola",
            Sku = "P100",
            Barcode = "8901234567890",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 1.00m,
            SalePrice = 1.50m
        };
        var firstRes = await _productService.CreateProductAsync(firstDto, 1, RoleDefaults.Admin);
        firstRes.Success.Should().BeTrue();

        // Act: Try creating another product with the same primary barcode
        var duplicateDto = new CreateProductDto
        {
            Name = "Diet Cola",
            Sku = "P101",
            Barcode = "8901234567890",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 1.00m,
            SalePrice = 1.50m
        };
        var dupRes = await _productService.CreateProductAsync(duplicateDto, 1, RoleDefaults.Admin);

        // Assert
        dupRes.Success.Should().BeFalse();
        dupRes.ErrorMessage.Should().Contain("8901234567890");
        dupRes.ErrorMessage.Should().Contain("Original Cola");
        dupRes.ErrorMessage.Should().Contain("P100");
    }

    [Fact]
    public async Task CreateProduct_WithOpeningStock_ShouldCreateExactlyOneOpeningMovementInSameTransaction()
    {
        // Arrange
        var dto = new CreateProductDto
        {
            Name = "Chocolate Bar",
            Sku = "CHOC-01",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 1.25m,
            SalePrice = 2.00m,
            OpeningQuantity = 25m
        };

        // Act
        var res = await _productService.CreateProductAsync(dto, 1, RoleDefaults.Admin);

        // Assert
        res.Success.Should().BeTrue();

        var movements = await _context.StockMovements.Where(sm => sm.ProductId == res.Data!.Id).ToListAsync();
        movements.Should().HaveCount(1);
        movements[0].MovementType.Should().Be(StockMovementType.Opening);
        movements[0].Quantity.Should().Be(25m);
        movements[0].UnitCost.Should().Be(1.25m);

        // Verify Product itself does not store a stock quantity column
        var paged = await _productRepo.GetPagedAsync(new ProductSearchQuery { SearchTerm = "Chocolate Bar" });
        paged.Items[0].CurrentStock.Should().Be(25m);
    }

    [Fact]
    public async Task PriceValidation_MinSalePriceGreaterThanSalePrice_ShouldBlock()
    {
        // Arrange
        var dto = new CreateProductDto
        {
            Name = "Overpriced Floor Product",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 10m,
            SalePrice = 12m,
            MinSalePrice = 15m // Invalid: MinSalePrice > SalePrice
        };

        // Act
        var res = await _productService.CreateProductAsync(dto, 1, RoleDefaults.Admin);

        // Assert
        res.Success.Should().BeFalse();
        res.ValidationErrors.Should().ContainKey("MinSalePrice");
    }

    [Fact]
    public async Task UpdateProduct_WhenPriceChanges_ShouldAutomaticallyRecordProductPriceHistory()
    {
        // Arrange
        var dto = new CreateProductDto
        {
            Name = "Coffee Beans 1kg",
            Sku = "COF-01",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 10.00m,
            SalePrice = 15.00m
        };
        var createRes = await _productService.CreateProductAsync(dto, 1, RoleDefaults.Admin);
        createRes.Success.Should().BeTrue();
        int prodId = createRes.Data!.Id;

        // Act: Update prices
        var updateDto = new UpdateProductDto
        {
            Id = prodId,
            Name = "Coffee Beans 1kg",
            Sku = "COF-01",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 11.50m, // Changed
            SalePrice = 17.00m, // Changed
            IsActive = true
        };
        var updateRes = await _productService.UpdateProductAsync(updateDto, _testUser.Id, RoleDefaults.Admin);
        updateRes.Success.Should().BeTrue();

        // Assert
        var histories = await _context.ProductPriceHistories.Where(h => h.ProductId == prodId).ToListAsync();
        histories.Should().HaveCount(1);
        histories[0].OldCostPrice.Should().Be(10.00m);
        histories[0].NewCostPrice.Should().Be(11.50m);
        histories[0].OldSalePrice.Should().Be(15.00m);
        histories[0].NewSalePrice.Should().Be(17.00m);
        histories[0].ChangedByUserId.Should().Be(_testUser.Id);
    }

    [Fact]
    public async Task UpdateProduct_WhenSeederChangesPriceWithNullUser_ShouldHaveNullChangedByUserId()
    {
        // Arrange
        var dto = new CreateProductDto
        {
            Name = "Tea Leaves 500g",
            Sku = "TEA-01",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 6.00m,
            SalePrice = 9.00m
        };
        var createRes = await _productService.CreateProductAsync(dto, 1, RoleDefaults.Admin);
        createRes.Success.Should().BeTrue();
        int prodId = createRes.Data!.Id;

        // Act: Update prices with null userId (system seeder / background process)
        var updateDto = new UpdateProductDto
        {
            Id = prodId,
            Name = "Tea Leaves 500g",
            Sku = "TEA-01",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 7.00m,
            SalePrice = 10.50m,
            IsActive = true
        };
        var updateRes = await _productService.UpdateProductAsync(updateDto, null, RoleDefaults.Admin);
        updateRes.Success.Should().BeTrue();

        // Assert: Price history was recorded and ChangedByUserId is null
        var histories = await _context.ProductPriceHistories.Where(h => h.ProductId == prodId).ToListAsync();
        histories.Should().HaveCount(1);
        histories[0].ChangedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProduct_WhenReferencedByMovements_ShouldBlockAndOfferDeactivate()
    {
        // Arrange: Product with opening movement
        var dto = new CreateProductDto
        {
            Name = "Cereal Box",
            Sku = "CER-01",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id,
            CostPrice = 3.00m,
            SalePrice = 4.50m,
            OpeningQuantity = 10m
        };
        var prod = await _productService.CreateProductAsync(dto, 1, RoleDefaults.Admin);

        // Act: Delete
        var delRes = await _productService.DeleteProductAsync(prod.Data!.Id, 1, RoleDefaults.Admin);

        // Assert
        delRes.Success.Should().BeFalse();
        delRes.ErrorMessage.Should().Be("REFERENCED_CANNOT_DELETE");

        // Act: Deactivate instead
        var deactRes = await _productService.DeactivateProductAsync(prod.Data!.Id, 1, RoleDefaults.Admin);
        deactRes.Success.Should().BeTrue();

        var refreshed = await _productRepo.GetByIdAsync(prod.Data!.Id);
        refreshed!.IsActive.Should().BeFalse();
        refreshed.IsDeleted.Should().BeFalse(); // Preserved in history
    }

    [Fact]
    public async Task CategoryHierarchy_CyclePrevention_ShouldPreventSelfOrDescendantAsParent()
    {
        // Arrange: Root -> Child -> Grandchild
        var rootRes = await _categoryService.CreateCategoryAsync(new CreateCategoryDto { Name = "Food" }, 1, RoleDefaults.Admin);
        var childRes = await _categoryService.CreateCategoryAsync(new CreateCategoryDto { Name = "Snacks", ParentCategoryId = rootRes.Data!.Id }, 1, RoleDefaults.Admin);
        var grandRes = await _categoryService.CreateCategoryAsync(new CreateCategoryDto { Name = "Chips", ParentCategoryId = childRes.Data!.Id }, 1, RoleDefaults.Admin);

        // Act: Attempt to set Root's parent to Grandchild (creating a cycle)
        var cycleRes = await _categoryService.UpdateCategoryAsync(new UpdateCategoryDto
        {
            Id = rootRes.Data!.Id,
            Name = "Food",
            ParentCategoryId = grandRes.Data!.Id
        }, 1, RoleDefaults.Admin);

        // Assert
        cycleRes.Success.Should().BeFalse();
        cycleRes.ErrorMessage.Should().Contain("cannot be its own ancestor");
    }

    [Fact]
    public async Task SingleUser_ShouldHaveAccessToAllServices()
    {
        // 1. User can create product
        var prodRes = await _productService.CreateProductAsync(new CreateProductDto
        {
            Name = "Authorized Product",
            CategoryId = _defaultCategory.Id,
            UnitId = _defaultUnit.Id
        }, 1, "Owner");
        prodRes.Success.Should().BeTrue();

        // 2. User can access suppliers
        var suppRes = await _supplierService.GetPagedSuppliersAsync(new MasterDataSearchQuery(), "Owner");
        suppRes.Success.Should().BeTrue();


    }
}

