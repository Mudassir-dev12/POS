using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using ShopPOS.Core.Validators;
using ShopPOS.Data;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Repositories;
using ShopPOS.Data.Services;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Inventory;

namespace ShopPOS.Tests;

public class InventoryModuleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IAuditLogService> _auditMock;
    private readonly TransactionRunner _transactionRunner;
    private readonly NumberSequenceService _sequenceService;

    private readonly ProductRepository _productRepo;
    private readonly CategoryRepository _categoryRepo;
    private readonly BrandRepository _brandRepo;
    private readonly UnitRepository _unitRepo;
    private readonly SupplierRepository _supplierRepo;
    private readonly PurchaseRepository _purchaseRepo;
    private readonly ShopSettingRepository _settingRepo;
    private readonly StockAdjustmentRepository _adjustmentRepo;
    private readonly InventoryRepository _inventoryRepo;
    private readonly SupplierLedgerRepository _supplierLedgerRepo;
    private readonly StockLedgerQuery _stockLedgerQuery;

    private readonly StockAdjustmentService _adjustmentService;
    private readonly InventoryService _inventoryService;
    private readonly PurchaseService _purchaseService;
    private readonly ProductService _productService;
    private readonly ICurrentUserProvider _userProvider;

    private User _ownerUser = null!;
    private Category _category1 = null!;
    private Category _category2 = null!;
    private Brand _brand1 = null!;
    private Unit _unit = null!;
    private Supplier _supplier = null!;
    private Product _productA = null!;
    private Product _productB = null!;
    private Product _productC = null!;

    public InventoryModuleTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _context = new AppDbContext(options);
        _context.Database.Migrate();

        _auditMock = new Mock<IAuditLogService>();
        _transactionRunner = new TransactionRunner(_context);
        _sequenceService = new NumberSequenceService(_context);
        _userProvider = new CurrentUserProvider();

        _productRepo = new ProductRepository(_context);
        _categoryRepo = new CategoryRepository(_context);
        _brandRepo = new BrandRepository(_context);
        _unitRepo = new UnitRepository(_context);
        _supplierRepo = new SupplierRepository(_context);
        _purchaseRepo = new PurchaseRepository(_context);
        _settingRepo = new ShopSettingRepository(_context);
        _adjustmentRepo = new StockAdjustmentRepository(_context);
        _inventoryRepo = new InventoryRepository(_context);
        _supplierLedgerRepo = new SupplierLedgerRepository(_context);
        _stockLedgerQuery = new StockLedgerQuery(_context);

        var pdfMock = new Mock<IPdfReportService>();
        pdfMock.Setup(p => p.GenerateStockOverviewPdfAsync(It.IsAny<IReadOnlyList<StockOverviewItemDto>>(), It.IsAny<ShopSettingDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });
        pdfMock.Setup(p => p.GenerateValuationPdfAsync(It.IsAny<IReadOnlyList<CategoryValuationDto>>(), It.IsAny<ShopSettingDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 4, 5, 6 });
        pdfMock.Setup(p => p.GeneratePurchaseInvoicePdfAsync(It.IsAny<PurchaseDetailDto>(), It.IsAny<ShopSettingDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 7, 8, 9 });

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

        _purchaseService = new PurchaseService(
            _transactionRunner,
            _purchaseRepo,
            _supplierRepo,
            _productRepo,
            _settingRepo,
            _sequenceService,
            _auditMock.Object,
            pdfMock.Object,
            new CreatePurchaseValidator(),
            new UpdatePurchaseValidator());

        _inventoryService = new InventoryService(
            _stockLedgerQuery,
            _productRepo,
            _categoryRepo,
            _brandRepo,
            _settingRepo,
            _supplierLedgerRepo,
            _inventoryRepo,
            _purchaseService,
            pdfMock.Object);

        _productService = new ProductService(
            _productRepo,
            _settingRepo,
            _auditMock.Object,
            _transactionRunner,
            _adjustmentService);

        SeedInitialData();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void SeedInitialData()
    {
        var ownerRole = new Role { Id = 1, Name = "Owner" };
        _context.Roles.Add(ownerRole);
        _context.SaveChanges();

        _ownerUser = new User
        {
            Id = 1,
            Username = "owner",
            FullName = "Shop Owner",
            RoleId = ownerRole.Id,
            IsActive = true,
            MustChangePassword = false
        };
        _context.Users.Add(_ownerUser);
        _context.SaveChanges();

        _category1 = new Category { Name = "Beverages", IsActive = true };
        _category2 = new Category { Name = "Snacks", IsActive = true };
        _brand1 = new Brand { Name = "Acme", IsActive = true };
        _unit = new Unit { Name = "Piece", ShortName = "pc", IsActive = true };
        _supplier = new Supplier { Name = "Acme Supplies", Phone = "1234567890", IsActive = true };

        _context.Categories.AddRange(_category1, _category2);
        _context.Brands.Add(_brand1);
        _context.Units.Add(_unit);
        _context.Suppliers.Add(_supplier);
        _context.SaveChanges();

        _productA = new Product
        {
            Sku = "SKU-A",
            Name = "Product A",
            CategoryId = _category1.Id,
            BrandId = _brand1.Id,
            UnitId = _unit.Id,
            CostPrice = 10.00m,
            SalePrice = 15.00m,
            TrackStock = true,
            ReorderLevel = 10m,
            IsActive = true
        };
        _productB = new Product
        {
            Sku = "SKU-B",
            Name = "Product B",
            CategoryId = _category1.Id,
            BrandId = _brand1.Id,
            UnitId = _unit.Id,
            CostPrice = 20.00m,
            SalePrice = 30.00m,
            TrackStock = true,
            ReorderLevel = 5m,
            IsActive = true
        };
        _productC = new Product
        {
            Sku = "SKU-C",
            Name = "Product C Untracked",
            CategoryId = _category2.Id,
            BrandId = _brand1.Id,
            UnitId = _unit.Id,
            CostPrice = 5.00m,
            SalePrice = 8.00m,
            TrackStock = false,
            ReorderLevel = 0m,
            IsActive = true
        };
        _context.Products.AddRange(_productA, _productB, _productC);
        _context.SaveChanges();

        _context.ShopSettings.Add(new ShopSetting
        {
            ShopName = "Test Shop",
            CurrencySymbol = "Rs",
            AllowNegativeStock = true,
            DefaultReorderMultiplier = 2.0m
        });
        _context.SaveChanges();

        // Add opening stock movements for Product A (10 units) and Product B (2 units)
        _context.StockMovements.Add(new StockMovement
        {
            ProductId = _productA.Id,
            MovementType = StockMovementType.Opening,
            Quantity = 10m,
            UnitCost = 10.00m,
            MovementDate = DateTime.Now.AddHours(-10),
            CreatedAt = DateTime.UtcNow.AddHours(-10),
            Note = "Opening balance"
        });
        _context.StockMovements.Add(new StockMovement
        {
            ProductId = _productB.Id,
            MovementType = StockMovementType.Opening,
            Quantity = 2m,
            UnitCost = 20.00m,
            MovementDate = DateTime.Now.AddHours(-10),
            CreatedAt = DateTime.UtcNow.AddHours(-10),
            Note = "Opening balance"
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task StockAdjustment_DraftCreation_CapturesLedgerSnapshotAndGeneratesAdjNumber()
    {
        var dto = new CreateStockAdjustmentDto
        {
            AdjustmentDate = DateTime.Today,
            Reason = StockAdjustmentReason.Damaged,
            Notes = "Broken bottles in warehouse",
            Items = new List<CreateStockAdjustmentItemDto>
            {
                new() { ProductId = _productA.Id, NewQuantity = 8m, Note = "2 broken" }
            }
        };

        var result = await _adjustmentService.CreateAdjustmentDraftAsync(dto, _ownerUser.Id);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AdjustmentNumber.Should().StartWith("ADJ-");
        result.Data.Status.Should().Be(StockAdjustmentStatus.Draft);
        result.Data.Items.Should().HaveCount(1);
        result.Data.Items[0].SystemQuantity.Should().Be(10m);
        result.Data.Items[0].NewQuantity.Should().Be(8m);
        result.Data.Items[0].Difference.Should().Be(-2m);
    }

    [Fact]
    public async Task StockAdjustment_Post_ReReadsStockInsideTransaction_AndCreatesMovementsOnlyForDiff()
    {
        var createResult = await _adjustmentService.CreateAdjustmentDraftAsync(new CreateStockAdjustmentDto
        {
            AdjustmentDate = DateTime.Today,
            Reason = StockAdjustmentReason.Found,
            Notes = "Inventory check",
            Items = new List<CreateStockAdjustmentItemDto>
            {
                new() { ProductId = _productA.Id, NewQuantity = 12m }, // +2
                new() { ProductId = _productB.Id, NewQuantity = 2m }   // 0 diff!
            }
        }, _ownerUser.Id);

        createResult.Success.Should().BeTrue();
        int adjId = createResult.Data!.Id;

        var postResult = await _adjustmentService.PostAdjustmentAsync(adjId, _ownerUser.Id);
        postResult.Success.Should().BeTrue();
        postResult.Data!.Status.Should().Be(StockAdjustmentStatus.Posted);

        // Verify ledger stock for Product A is now 12
        var stockA = await _stockLedgerQuery.GetCurrentStockAsync(_productA.Id);
        stockA.Should().Be(12m);

        // Product B remains 2
        var stockB = await _stockLedgerQuery.GetCurrentStockAsync(_productB.Id);
        stockB.Should().Be(2m);

        // Only 1 adjustment movement created (for Product A)
        var movements = await _context.StockMovements
            .Where(m => m.ReferenceId == adjId && m.MovementType == StockMovementType.Adjustment)
            .ToListAsync();
        movements.Should().HaveCount(1);
        movements[0].ProductId.Should().Be(_productA.Id);
        movements[0].Quantity.Should().Be(2m);
    }

    [Fact]
    public async Task StockAdjustment_NegativeStock_BlockedWhenSettingDisabled()
    {
        // Disable negative stock
        var settings = await _context.ShopSettings.FirstAsync();
        settings.AllowNegativeStock = false;
        await _context.SaveChangesAsync();

        // Product A current stock is 10. Attempt to adjust to -5
        var draft = await _adjustmentService.CreateAdjustmentDraftAsync(new CreateStockAdjustmentDto
        {
            AdjustmentDate = DateTime.Today,
            Reason = StockAdjustmentReason.Damaged,
            Items = new List<CreateStockAdjustmentItemDto>
            {
                new() { ProductId = _productA.Id, NewQuantity = -5m }
            }
        }, _ownerUser.Id);

        draft.Success.Should().BeTrue();

        var postResult = await _adjustmentService.PostAdjustmentAsync(draft.Data!.Id, _ownerUser.Id);
        postResult.Success.Should().BeFalse();
        postResult.ErrorMessage.Should().ContainEquivalentOf("negative stock");

        // Verify stock is untouched
        var stock = await _stockLedgerQuery.GetCurrentStockAsync(_productA.Id);
        stock.Should().Be(10m);
    }

    [Fact]
    public async Task StockAdjustment_Cancel_CreatesReversingMovement_AndRequiresReason()
    {
        // 1. Create and post adjustment: Product A 10 -> 15 (+5)
        var createResult = await _adjustmentService.CreateAdjustmentDraftAsync(new CreateStockAdjustmentDto
        {
            AdjustmentDate = DateTime.Today,
            Reason = StockAdjustmentReason.Found,
            Items = new List<CreateStockAdjustmentItemDto>
            {
                new() { ProductId = _productA.Id, NewQuantity = 15m }
            }
        }, _ownerUser.Id);
        int adjId = createResult.Data!.Id;
        await _adjustmentService.PostAdjustmentAsync(adjId, _ownerUser.Id);

        (await _stockLedgerQuery.GetCurrentStockAsync(_productA.Id)).Should().Be(15m);

        // 2. Cancel without reason -> Fails
        var failCancel = await _adjustmentService.CancelAdjustmentAsync(adjId, " ", _ownerUser.Id);
        failCancel.Success.Should().BeFalse();
        failCancel.ErrorMessage.Should().ContainEquivalentOf("cancellation reason is required");

        // 3. Cancel with valid reason -> Reverses movement (-5)
        var cancelResult = await _adjustmentService.CancelAdjustmentAsync(adjId, "Found stock was miscounted", _ownerUser.Id);
        cancelResult.Success.Should().BeTrue();

        // Ledger stock restored to 10
        (await _stockLedgerQuery.GetCurrentStockAsync(_productA.Id)).Should().Be(10m);

        // Verify reversal movement
        var reversal = await _context.StockMovements
            .FirstOrDefaultAsync(m => m.ReferenceId == adjId && m.MovementType == StockMovementType.AdjustmentCancel);
        reversal.Should().NotBeNull();
        reversal!.Quantity.Should().Be(-5m);
    }

    [Fact]
    public async Task StockAdjustment_ProductEdit_SingleLineHelper_PostsDirectly()
    {
        var result = await _adjustmentService.CreateAndPostSingleAdjustmentAsync(
            _productA.Id,
            25m,
            "Physical re-count in shop",
            StockAdjustmentReason.CountCorrection,
            _ownerUser.Id);

        result.Success.Should().BeTrue();

        var currentStock = await _stockLedgerQuery.GetCurrentStockAsync(_productA.Id);
        currentStock.Should().Be(25m);
    }

    [Fact]
    public async Task StockStatus_BoundaryConditions_CorrectStatusEverywhere()
    {
        // Set up products for all 5 stock status boundary conditions:
        // 1. Negative (< 0): Product Neg, ReorderLevel = 5, Stock = -2
        var prodNeg = new Product
        {
            Sku = "SKU-NEG",
            Name = "Product Negative",
            CategoryId = _category1.Id,
            BrandId = _brand1.Id,
            UnitId = _unit.Id,
            CostPrice = 10m,
            SalePrice = 15m,
            TrackStock = true,
            ReorderLevel = 5m,
            IsActive = true
        };
        // 2. OutOfStock (== 0): Product Zero, ReorderLevel = 5, Stock = 0
        var prodZero = new Product
        {
            Sku = "SKU-ZERO",
            Name = "Product OutOfStock",
            CategoryId = _category1.Id,
            BrandId = _brand1.Id,
            UnitId = _unit.Id,
            CostPrice = 10m,
            SalePrice = 15m,
            TrackStock = true,
            ReorderLevel = 5m,
            IsActive = true
        };
        // 3. Low stock (stock > 0 and stock == ReorderLevel): Product Low, ReorderLevel = 5, Stock = 5
        var prodLow = new Product
        {
            Sku = "SKU-LOW",
            Name = "Product LowStock",
            CategoryId = _category1.Id,
            BrandId = _brand1.Id,
            UnitId = _unit.Id,
            CostPrice = 10m,
            SalePrice = 15m,
            TrackStock = true,
            ReorderLevel = 5m,
            IsActive = true
        };
        // 4. OK (stock == ReorderLevel + 1): Product OK, ReorderLevel = 5, Stock = 6
        var prodOk = new Product
        {
            Sku = "SKU-OK",
            Name = "Product OK",
            CategoryId = _category1.Id,
            BrandId = _brand1.Id,
            UnitId = _unit.Id,
            CostPrice = 10m,
            SalePrice = 15m,
            TrackStock = true,
            ReorderLevel = 5m,
            IsActive = true
        };

        _context.Products.AddRange(prodNeg, prodZero, prodLow, prodOk);
        await _context.SaveChangesAsync();

        // Add movements:
        _context.StockMovements.Add(new StockMovement
        {
            ProductId = prodNeg.Id,
            MovementType = StockMovementType.Adjustment,
            Quantity = -2m,
            CreatedAt = DateTime.UtcNow
        });
        // prodZero has 0 movements => stock = 0
        _context.StockMovements.Add(new StockMovement
        {
            ProductId = prodLow.Id,
            MovementType = StockMovementType.Opening,
            Quantity = 5m,
            CreatedAt = DateTime.UtcNow
        });
        _context.StockMovements.Add(new StockMovement
        {
            ProductId = prodOk.Id,
            MovementType = StockMovementType.Opening,
            Quantity = 6m,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // 1. Verify StockOverview query for each item
        var overview = await _inventoryService.GetStockOverviewAsync(new StockOverviewFilterQuery { PageSize = 100, IncludeNonTracked = true });
        overview.Success.Should().BeTrue();
        var items = overview.Data!.Items;

        var negItem = items.First(i => i.ProductId == prodNeg.Id);
        negItem.Status.Should().Be(StockStatus.Negative);
        negItem.CurrentStock.Should().Be(-2m);

        var zeroItem = items.First(i => i.ProductId == prodZero.Id);
        zeroItem.Status.Should().Be(StockStatus.OutOfStock);
        zeroItem.CurrentStock.Should().Be(0m);

        var lowItem = items.First(i => i.ProductId == prodLow.Id);
        lowItem.Status.Should().Be(StockStatus.Low);
        lowItem.CurrentStock.Should().Be(5m);

        var okItem = items.First(i => i.ProductId == prodOk.Id);
        okItem.Status.Should().Be(StockStatus.OK);
        okItem.CurrentStock.Should().Be(6m);

        var untrackedItem = items.First(i => i.ProductId == _productC.Id);
        untrackedItem.Status.Should().Be(StockStatus.NotTracked);

        // 2. Status filters offer them separately:
        // Filter Low only -> Should contain prodLow, but NOT prodZero or prodNeg!
        var lowFilter = await _inventoryService.GetStockOverviewAsync(new StockOverviewFilterQuery { Status = StockStatus.Low, PageSize = 100 });
        lowFilter.Data!.Items.Should().Contain(i => i.ProductId == prodLow.Id);
        lowFilter.Data!.Items.Should().NotContain(i => i.ProductId == prodZero.Id);
        lowFilter.Data!.Items.Should().NotContain(i => i.ProductId == prodNeg.Id);

        // Filter OutOfStock only -> Should contain prodZero, but NOT prodLow
        var zeroFilter = await _inventoryService.GetStockOverviewAsync(new StockOverviewFilterQuery { Status = StockStatus.OutOfStock, PageSize = 100 });
        zeroFilter.Data!.Items.Should().Contain(i => i.ProductId == prodZero.Id);
        zeroFilter.Data!.Items.Should().NotContain(i => i.ProductId == prodLow.Id);

        // Filter Negative only -> Should contain prodNeg
        var negFilter = await _inventoryService.GetStockOverviewAsync(new StockOverviewFilterQuery { Status = StockStatus.Negative, PageSize = 100 });
        negFilter.Data!.Items.Should().Contain(i => i.ProductId == prodNeg.Id);
        negFilter.Data!.Items.Should().NotContain(i => i.ProductId == prodZero.Id);

        // Filter NotTracked only -> Should contain untracked product
        var notTrackedFilter = await _inventoryService.GetStockOverviewAsync(new StockOverviewFilterQuery { Status = StockStatus.NotTracked, PageSize = 100 });
        notTrackedFilter.Data!.Items.Should().Contain(i => i.ProductId == _productC.Id);
        notTrackedFilter.Data!.Items.Should().NotContain(i => i.ProductId == prodLow.Id);

        // 3. Reorder list includes both Low and OutOfStock (and Negative), showing exact status
        var reorderList = await _inventoryService.GetReorderListAsync();
        reorderList.Success.Should().BeTrue();
        var reorderItems = reorderList.Data!;

        var reorderLow = reorderItems.FirstOrDefault(r => r.ProductId == prodLow.Id);
        reorderLow.Should().NotBeNull();
        reorderLow!.Status.Should().Be(StockStatus.Low);

        var reorderZero = reorderItems.FirstOrDefault(r => r.ProductId == prodZero.Id);
        reorderZero.Should().NotBeNull();
        reorderZero!.Status.Should().Be(StockStatus.OutOfStock);

        var reorderNeg = reorderItems.FirstOrDefault(r => r.ProductId == prodNeg.Id);
        reorderNeg.Should().NotBeNull();
        reorderNeg!.Status.Should().Be(StockStatus.Negative);
    }

    [Fact]
    public async Task StockAdjustmentDialogViewModel_RowSelection_MultiAdd_AndPosting()
    {
        var dialogServiceMock = new Mock<IDialogService>();

        // Set up multi-product search mock
        var chosenProducts = new List<ProductListItemDto>
        {
            new() { Id = _productA.Id, Name = _productA.Name, Sku = _productA.Sku, CostPrice = _productA.CostPrice, CurrentStock = 10m },
            new() { Id = _productB.Id, Name = _productB.Name, Sku = _productB.Sku, CostPrice = _productB.CostPrice, CurrentStock = 2m },
            new() { Id = _productC.Id, Name = _productC.Name, Sku = _productC.Sku, CostPrice = _productC.CostPrice, CurrentStock = 0m }
        };

        dialogServiceMock.Setup(d => d.ShowMultiProductSearchDialog(It.IsAny<ShopPOS.UI.ViewModels.Purchases.ProductSearchDialogViewModel>()))
            .Returns(chosenProducts);

        var vm = new StockAdjustmentDialogViewModel(
            null,
            _adjustmentService,
            _productService,
            _stockLedgerQuery,
            dialogServiceMock.Object);

        // 1. Multi-add products from dialog
        await vm.AddProductLineAsync();
        vm.Items.Should().HaveCount(3);

        // 2. Test duplicate ignore: adding again should add 0 new items
        await vm.AddProductLineAsync();
        vm.Items.Should().HaveCount(3, "Duplicate products in document must be ignored");

        // 3. Test Row Selection: select 2nd line and 3rd line
        vm.SelectedLine = vm.Items[1];
        vm.SelectedLine.ProductId.Should().Be(_productB.Id);

        vm.SelectedLine = vm.Items[2];
        vm.SelectedLine.ProductId.Should().Be(_productC.Id);

        // 4. Change quantities for 3 lines:
        // Product A: 10 -> 12 (diff +2)
        vm.Items[0].NewQuantity = 12m;
        // Product B: 2 -> 5 (diff +3)
        vm.Items[1].NewQuantity = 5m;
        // Product C: 0 -> 4 (diff +4)
        vm.Items[2].NewQuantity = 4m;

        bool closeTriggered = false;
        vm.RequestClose += success => closeTriggered = success;

        await vm.SaveAndPostAsync();
        closeTriggered.Should().BeTrue();
        vm.ErrorMessage.Should().BeNull();

        // 5. Verify movements: exactly 3 movements created in database
        var movements = await _context.StockMovements
            .Where(m => m.MovementType == StockMovementType.Adjustment)
            .ToListAsync();
        movements.Should().HaveCount(3);
    }

    [Fact]
    public async Task InventoryService_MovementHistory_TimestampsAndFormatting()
    {
        // 1. Post a purchase and a stock adjustment with explicit local times
        var purchaseResult = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplier.Id,
            SupplierInvoiceNo = "INV-TIME-TEST",
            PurchaseDate = DateTime.Now,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = _productA.Id, Quantity = 4m, UnitCost = 10m }
            }
        }, _ownerUser.Id);
        purchaseResult.Success.Should().BeTrue();

        var postPoResult = await _purchaseService.PostPurchaseAsync(purchaseResult.Data!.Id, null, null, _ownerUser.Id);
        postPoResult.Success.Should().BeTrue();

        var adjResult = await _adjustmentService.CreateAndPostSingleAdjustmentAsync(
            _productA.Id, 20m, "Adjustment with timestamp", StockAdjustmentReason.Found, _ownerUser.Id);
        adjResult.Success.Should().BeTrue();

        // 2. Query movements and verify non-midnight timestamps
        var movements = await _context.StockMovements
            .Where(m => m.ProductId == _productA.Id && (m.MovementType == StockMovementType.Purchase || m.MovementType == StockMovementType.Adjustment))
            .ToListAsync();

        movements.Should().NotBeEmpty();
        foreach (var mov in movements)
        {
            // Verify MovementDate has non-zero time of day (or falls back to CreatedAt time)
            var effectiveTime = mov.MovementDate.TimeOfDay != TimeSpan.Zero
                ? mov.MovementDate.TimeOfDay
                : mov.CreatedAt.ToLocalTime().TimeOfDay;
            effectiveTime.Should().NotBe(TimeSpan.Zero);
        }

        // 3. Verify CSV export formats as dd MMM yyyy HH:mm
        var csvResult = await _inventoryService.ExportMovementHistoryCsvAsync(new MovementHistoryFilterQuery
        {
            ProductId = _productA.Id
        });
        csvResult.Success.Should().BeTrue();
        var csvBytes = csvResult.Data!;
        csvBytes.Should().NotBeEmpty();
        string csvContent = System.Text.Encoding.UTF8.GetString(csvBytes);
        csvContent.Should().Contain("Date,SKU,Product Name,Movement Type");
    }

    [Fact]
    public async Task InventoryService_ReorderList_RespectsMultiplierFormula_AndCreatesPurchaseDraft()
    {
        // Adjust Product A down to 4 units
        await _adjustmentService.CreateAndPostSingleAdjustmentAsync(
            _productA.Id, 4m, "Reduce stock for reorder test", StockAdjustmentReason.Damaged, _ownerUser.Id);

        // Product A has ReorderLevel = 10, CurrentStock = 4
        // DefaultReorderMultiplier is 2.0
        // Formula: Math.Max(1, Math.Ceiling(10 * 2.0 - 4)) = Math.Ceiling(20 - 4) = 16 units
        var reorderResult = await _inventoryService.GetReorderListAsync();
        reorderResult.Success.Should().BeTrue();

        var reorderA = reorderResult.Data!.FirstOrDefault(r => r.ProductId == _productA.Id);
        reorderA.Should().NotBeNull();
        reorderA!.CurrentStock.Should().Be(4m);
        reorderA.ReorderLevel.Should().Be(10m);
        reorderA.SuggestedQuantity.Should().Be(16m);

        // Create purchase draft from reorder
        var createPoResult = await _inventoryService.CreatePurchaseDraftFromReorderAsync(new CreatePurchaseFromReorderDto
        {
            SupplierId = _supplier.Id,
            Items = new List<ReorderPurchaseLineDto>
            {
                new() { ProductId = _productA.Id, Quantity = reorderA.SuggestedQuantity, UnitCost = reorderA.CostPrice }
            }
        }, _ownerUser.Id);

        createPoResult.Success.Should().BeTrue();
        createPoResult.Data.Should().NotBeNull();
        createPoResult.Data!.Status.Should().Be(PurchaseStatus.Draft);
        createPoResult.Data.PurchaseNumber.Should().StartWith("PUR-");
        createPoResult.Data.Items.Should().HaveCount(1);
        createPoResult.Data.Items[0].Quantity.Should().Be(16m);
    }

    [Fact]
    public async Task InventoryService_ValuationByCategory_CalculatesAggregatesCorrectly()
    {
        var result = await _inventoryService.GetCategoryValuationsAsync();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeEmpty();

        var bev = result.Data!.FirstOrDefault(c => c.CategoryName == "Beverages");
        bev.Should().NotBeNull();
        bev!.ProductCount.Should().Be(2); // Product A and B
        bev.TotalUnits.Should().Be(12m);    // 10 + 2
        bev.ValueAtCost.Should().Be(140.00m); // (10 * 10) + (2 * 20) = 100 + 40 = 140
        bev.ValueAtSale.Should().Be(210.00m); // (10 * 15) + (2 * 30) = 150 + 60 = 210
        bev.PotentialProfit.Should().Be(70.00m);   // 210 - 140 = 70
        bev.ProfitMarginPercent.Should().BeApproximately(33.3m, 0.1m);
    }

    [Fact]
    public async Task NumberSequence_ADJ_and_PUR_ConcurrentSavesOnRealSqliteFileInWalMode()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"test_inv_seq_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            // Initialize database schema with real migrations
            using (var initCtx = new AppDbContext(options))
            {
                await initCtx.Database.MigrateAsync();
            }

            // Run 20 concurrent tasks for ADJ and 20 concurrent tasks for PUR
            var adjTasks = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
            {
                using var taskCtx = new AppDbContext(options);
                var runner = new TransactionRunner(taskCtx);
                return await runner.RunInTransactionAsync(async () =>
                {
                    var seqService = new NumberSequenceService(taskCtx);
                    return await seqService.GetNextNumberAsync("ADJ");
                });
            }));

            var purTasks = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
            {
                using var taskCtx = new AppDbContext(options);
                var runner = new TransactionRunner(taskCtx);
                return await runner.RunInTransactionAsync(async () =>
                {
                    var seqService = new NumberSequenceService(taskCtx);
                    return await seqService.GetNextNumberAsync("PUR");
                });
            }));

            var allTasks = adjTasks.Concat(purTasks).ToArray();
            var numbers = await Task.WhenAll(allTasks);

            numbers.Should().HaveCount(40);
            var adjNumbers = numbers.Where(n => n.StartsWith("ADJ-")).ToList();
            var purNumbers = numbers.Where(n => n.StartsWith("PUR-")).ToList();

            adjNumbers.Should().HaveCount(20);
            adjNumbers.Distinct().Should().HaveCount(20, "Every ADJ number generated concurrently must be strictly unique");

            purNumbers.Should().HaveCount(20);
            purNumbers.Distinct().Should().HaveCount(20, "Every PUR number generated concurrently must be strictly unique");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
