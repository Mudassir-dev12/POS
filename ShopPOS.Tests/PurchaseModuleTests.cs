using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
using System.Diagnostics;

namespace ShopPOS.Tests;

public class PurchaseModuleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly PurchaseRepository _purchaseRepo;
    private readonly SupplierPaymentRepository _paymentRepo;
    private readonly PurchaseReturnRepository _returnRepo;
    private readonly SupplierLedgerRepository _ledgerRepo;
    private readonly SupplierRepository _supplierRepo;
    private readonly ShopSettingRepository _shopSettingRepo;
    private readonly ProductRepository _productRepo;
    private readonly Mock<IAuditLogService> _auditMock;
    private readonly NumberSequenceService _sequenceService;
    private readonly TransactionRunner _transactionRunner;
    private readonly PurchaseCalculatorService _calculatorService;

    private readonly PurchaseService _purchaseService;
    private readonly SupplierPaymentService _paymentService;
    private readonly PurchaseReturnService _returnService;
    private readonly SupplierLedgerService _ledgerService;

    private User _adminUser = null!;
    private User _managerUser = null!;
    private User _cashierUser = null!;
    private Supplier _supplierA = null!;
    private Supplier _supplierB = null!;
    private Product _product1 = null!;
    private Product _product2 = null!;

    public PurchaseModuleTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _context = new AppDbContext(options);
        _context.Database.Migrate();

        _purchaseRepo = new PurchaseRepository(_context);
        _paymentRepo = new SupplierPaymentRepository(_context);
        _returnRepo = new PurchaseReturnRepository(_context);
        _ledgerRepo = new SupplierLedgerRepository(_context);
        _supplierRepo = new SupplierRepository(_context);
        _shopSettingRepo = new ShopSettingRepository(_context);
        _productRepo = new ProductRepository(_context);

        _auditMock = new Mock<IAuditLogService>();
        _sequenceService = new NumberSequenceService(_context);
        _transactionRunner = new TransactionRunner(_context);
        _calculatorService = new PurchaseCalculatorService();

        var pdfMock = new Mock<IPdfReportService>();
        pdfMock.Setup(p => p.GeneratePurchaseInvoicePdfAsync(It.IsAny<PurchaseDetailDto>(), It.IsAny<ShopSettingDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        _purchaseService = new PurchaseService(
            _transactionRunner,
            _purchaseRepo,
            _supplierRepo,
            _productRepo,
            _shopSettingRepo,
            _sequenceService,
            _auditMock.Object,
            pdfMock.Object,
            new ShopPOS.Core.Validators.CreatePurchaseValidator(),
            new ShopPOS.Core.Validators.UpdatePurchaseValidator());

        _paymentService = new SupplierPaymentService(
            _transactionRunner,
            _paymentRepo,
            _supplierRepo,
            _purchaseRepo,
            _sequenceService,
            _auditMock.Object,
            new ShopPOS.Core.Validators.CreateSupplierPaymentValidator());

        _returnService = new PurchaseReturnService(
            _transactionRunner,
            _returnRepo,
            _purchaseRepo,
            _supplierRepo,
            _shopSettingRepo,
            _sequenceService,
            _auditMock.Object,
            new ShopPOS.Core.Validators.CreatePurchaseReturnValidator());

        _ledgerService = new SupplierLedgerService(
            _ledgerRepo,
            _supplierRepo,
            _shopSettingRepo,
            pdfMock.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var adminRole = new Role { Name = RoleDefaults.Admin };
        var managerRole = new Role { Name = RoleDefaults.Manager };
        var cashierRole = new Role { Name = RoleDefaults.Cashier };
        _context.Roles.AddRange(adminRole, managerRole, cashierRole);

        _adminUser = new User { Username = "admin", FullName = "System Admin", PasswordHash = "h", Role = adminRole, IsActive = true };
        _managerUser = new User { Username = "manager", FullName = "Store Manager", PasswordHash = "h", Role = managerRole, IsActive = true };
        _cashierUser = new User { Username = "cashier", FullName = "Front Cashier", PasswordHash = "h", Role = cashierRole, IsActive = true };
        _context.Users.AddRange(_adminUser, _managerUser, _cashierUser);

        _supplierA = new Supplier { Name = "Acme Supplies", Phone = "111-222", IsActive = true, OpeningBalance = 0m };
        _supplierB = new Supplier { Name = "Beta Distributors", Phone = "333-444", IsActive = true, OpeningBalance = 200m };
        _context.Suppliers.AddRange(_supplierA, _supplierB);

        var unit = new Unit { Name = "Pieces", ShortName = "pcs", IsActive = true };
        var cat = new Category { Name = "Groceries", IsActive = true };
        _context.Units.Add(unit);
        _context.Categories.Add(cat);

        _product1 = new Product
        {
            Name = "Organic Milk 1L",
            Sku = "P-MILK-001",
            Barcode = "890111",
            CostPrice = 10.00m,
            SalePrice = 15.00m,
            TrackStock = true,
            Unit = unit,
            Category = cat,
            IsActive = true
        };

        _product2 = new Product
        {
            Name = "Whole Wheat Bread",
            Sku = "P-BREAD-001",
            Barcode = "890222",
            CostPrice = 20.00m,
            SalePrice = 30.00m,
            TrackStock = true,
            Unit = unit,
            Category = cat,
            IsActive = true
        };

        _context.Products.AddRange(_product1, _product2);

        var setting = new ShopSetting
        {
            ShopName = "Test Shop",
            CostingMethod = CostingMethod.WeightedAverage,
            AllowNegativeStock = false
        };
        _context.ShopSettings.Add(setting);

        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public void Test01_PurchaseCalculator_DiscountsTaxFreightAndRounding_ComputesCorrectly()
    {
        // Line calculation: qty 3, cost 10.55, 10% discount, 5% tax
        // Gross: 3 * 10.55 = 31.65
        // Discount: 31.65 * 0.10 = 3.165 -> 3.17
        // Net: 31.65 - 3.17 = 28.48
        // Tax: 28.48 * 0.05 = 1.424 -> 1.42
        // LineTotal: 28.48 + 1.42 = 29.90
        var line = _calculatorService.CalculateLine(3m, 10.55m, 10m, 5m);

        line.GrossAmount.Should().Be(31.65m);
        line.DiscountAmount.Should().Be(3.17m);
        line.NetAmount.Should().Be(28.48m);
        line.TaxAmount.Should().Be(1.42m);
        line.LineTotal.Should().Be(29.90m);

        // Totals calculation
        var totals = _calculatorService.CalculateTotals(new[] { line }, invoiceDiscount: 5.00m, freightAmount: 12.50m);
        // SubTotal: 28.48, Tax: 1.42, Discount: 5.00, Freight: 12.50
        // GrandTotal: 28.48 - 5.00 + 1.42 + 12.50 = 37.40
        totals.SubTotal.Should().Be(28.48m);
        totals.DiscountAmount.Should().Be(5.00m);
        totals.TaxAmount.Should().Be(1.42m);
        totals.FreightAmount.Should().Be(12.50m);
        totals.GrandTotal.Should().Be(37.40m);

        // Half-away-from-zero rounding
        _calculatorService.Round(1.005m).Should().Be(1.01m);
        _calculatorService.Round(1.004m).Should().Be(1.00m);
        _calculatorService.Round(1.015m).Should().Be(1.02m);
        _calculatorService.Round(-1.005m).Should().Be(-1.01m);
    }

    [Fact]
    public async Task Test02_PostPurchase_CreatesMovements_AndUpdatesCosts_ForBothCostingMethods_IncludingZeroStockStart()
    {
        // Part A: Zero-stock start with WeightedAverage
        var createDraft = new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            SupplierInvoiceNo = "INV-001",
            PurchaseDate = DateTime.Today,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = _product1.Id, Quantity = 5m, UnitCost = 20.00m }
            }
        };

        var draftRes = await _purchaseService.CreatePurchaseDraftAsync(createDraft, _adminUser.Id);
        draftRes.Success.Should().BeTrue();

        var postRes = await _purchaseService.PostPurchaseAsync(draftRes.Data!.Id, null, null, _adminUser.Id);
        postRes.Success.Should().BeTrue();

        // Stock movement verified
        var movements = await _context.StockMovements.Where(sm => sm.ProductId == _product1.Id).ToListAsync();
        movements.Should().HaveCount(1);
        movements[0].MovementType.Should().Be(StockMovementType.Purchase);
        movements[0].Quantity.Should().Be(5m);
        movements[0].UnitCost.Should().Be(20.00m);

        // Product cost updated to 20.00 from 10.00
        var prod1After = await _context.Products.FindAsync(_product1.Id);
        prod1After!.CostPrice.Should().Be(20.00m);

        // Price history written
        var history = await _context.ProductPriceHistories.Where(h => h.ProductId == _product1.Id).ToListAsync();
        history.Should().HaveCount(1);
        history[0].OldCostPrice.Should().Be(10.00m);
        history[0].NewCostPrice.Should().Be(20.00m);
        history[0].ChangedByUserId.Should().Be(_adminUser.Id);

        // Part B: Subsequent purchase with WeightedAverage
        // Current: 5 units @ 20.00 = 100.00
        // New: 15 units @ 28.00 = 420.00
        // Total value = 520.00 / 20 = 26.00
        var createDraft2 = new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            SupplierInvoiceNo = "INV-002",
            PurchaseDate = DateTime.Today,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = _product1.Id, Quantity = 15m, UnitCost = 28.00m }
            }
        };

        var draft2 = await _purchaseService.CreatePurchaseDraftAsync(createDraft2, _adminUser.Id);
        var post2 = await _purchaseService.PostPurchaseAsync(draft2.Data!.Id, null, null, _adminUser.Id);
        post2.Success.Should().BeTrue();

        var prod1After2 = await _context.Products.FindAsync(_product1.Id);
        prod1After2!.CostPrice.Should().Be(26.00m);

        // Part C: LastCost method
        var settings = await _context.ShopSettings.FirstAsync();
        settings.CostingMethod = CostingMethod.LastCost;
        await _context.SaveChangesAsync();

        var createDraft3 = new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            SupplierInvoiceNo = "INV-003",
            PurchaseDate = DateTime.Today,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = _product1.Id, Quantity = 10m, UnitCost = 33.00m, DiscountPercent = 10m } // Net = 29.70
            }
        };

        var draft3 = await _purchaseService.CreatePurchaseDraftAsync(createDraft3, _adminUser.Id);
        var post3 = await _purchaseService.PostPurchaseAsync(draft3.Data!.Id, null, null, _adminUser.Id);
        post3.Success.Should().BeTrue();

        var prod1After3 = await _context.Products.FindAsync(_product1.Id);
        prod1After3!.CostPrice.Should().Be(29.70m);
    }

    [Fact]
    public async Task Test03_PostPurchase_IsAtomic_RollsBackOnFailure()
    {
        var createDraft = new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            PurchaseDate = DateTime.Today,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = _product1.Id, Quantity = 10m, UnitCost = 15.00m }
            }
        };

        var draft = await _purchaseService.CreatePurchaseDraftAsync(createDraft, _adminUser.Id);
        draft.Success.Should().BeTrue();

        // Attempt to post with payNow amount that exceeds GrandTotal (triggers failure inside transaction)
        var postRes = await _purchaseService.PostPurchaseAsync(draft.Data!.Id, payNowAmount: 999999m, PaymentMethod.Cash, _adminUser.Id);
        postRes.Success.Should().BeFalse();

        // Assert nothing was saved
        var purchaseInDb = await _context.Purchases.FindAsync(draft.Data!.Id);
        purchaseInDb!.Status.Should().Be(PurchaseStatus.Draft);
        purchaseInDb.PaidAmount.Should().Be(0m);

        var movements = await _context.StockMovements.Where(sm => sm.ProductId == _product1.Id).ToListAsync();
        movements.Should().BeEmpty();
    }

    [Fact]
    public async Task Test04_PostedPurchase_CannotBeEditedOrDeleted()
    {
        var draft = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 5m, UnitCost = 10m } }
        }, _adminUser.Id);

        var postRes = await _purchaseService.PostPurchaseAsync(draft.Data!.Id, null, null, _adminUser.Id);
        postRes.Success.Should().BeTrue();

        // Edit attempt fails
        var editRes = await _purchaseService.UpdatePurchaseDraftAsync(new UpdatePurchaseDto
        {
            Id = draft.Data!.Id,
            SupplierId = _supplierA.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 8m, UnitCost = 12m } }
        }, _adminUser.Id);
        editRes.Success.Should().BeFalse();
        editRes.ErrorMessage.Should().Contain("draft");

        // Delete attempt fails
        var delRes = await _purchaseService.DeletePurchaseDraftAsync(draft.Data!.Id, _adminUser.Id);
        delRes.Success.Should().BeFalse();
        delRes.ErrorMessage.Should().Contain("draft");
    }

    [Fact]
    public async Task Test05_CancelPurchase_CreatesReversingMovements_AndKeepsOriginals()
    {
        var draft = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 10m, UnitCost = 15m } }
        }, _adminUser.Id);

        await _purchaseService.PostPurchaseAsync(draft.Data!.Id, null, null, _adminUser.Id);

        // Cancel with reason
        var cancelRes = await _purchaseService.CancelPurchaseAsync(draft.Data!.Id, "Vendor cancelled order", _adminUser.Id);
        cancelRes.Success.Should().BeTrue();

        var movements = await _context.StockMovements.Where(sm => sm.ProductId == _product1.Id).OrderBy(sm => sm.Id).ToListAsync();
        movements.Should().HaveCount(2);

        // Original preserved
        movements[0].MovementType.Should().Be(StockMovementType.Purchase);
        movements[0].Quantity.Should().Be(10m);

        // Reversal added
        movements[1].MovementType.Should().Be(StockMovementType.PurchaseCancel);
        movements[1].Quantity.Should().Be(-10m);

        var purchaseInDb = await _context.Purchases.FindAsync(draft.Data!.Id);
        purchaseInDb!.Status.Should().Be(PurchaseStatus.Cancelled);
        purchaseInDb.CancelReason.Should().Be("Vendor cancelled order");
        purchaseInDb.CancelledByUserId.Should().Be(_adminUser.Id);

        // Net stock is 0
        var currentStock = await _purchaseRepo.GetProductCurrentStockAsync(_product1.Id);
        currentStock.Should().Be(0m);
    }

    [Fact]
    public async Task Test06_CancelPurchase_BlockedWithPayments_OrWhenStockWouldGoNegative()
    {
        var draft = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 10m, UnitCost = 15m } }
        }, _adminUser.Id);

        await _purchaseService.PostPurchaseAsync(draft.Data!.Id, null, null, _adminUser.Id);

        // 1. Record an active payment
        var payRes = await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = _supplierA.Id,
            PurchaseId = draft.Data!.Id,
            Amount = 50.00m
        }, _adminUser.Id);
        payRes.Success.Should().BeTrue();

        // 2. Cancellation blocked due to active payment
        var cancelFail = await _purchaseService.CancelPurchaseAsync(draft.Data!.Id, "Cancel attempt", _adminUser.Id);
        cancelFail.Success.Should().BeFalse();
        cancelFail.ErrorMessage.Should().Contain("payments");

        // Cancel payment first
        await _paymentService.CancelPaymentAsync(payRes.Data!.Id, "Refunded", _adminUser.Id);

        // 3. Simulate stock reduction (sold 8 items, only 2 remaining on hand)
        _context.StockMovements.Add(new StockMovement
        {
            ProductId = _product1.Id,
            MovementType = StockMovementType.Sale,
            Quantity = -8m,
            UnitCost = 15m,
            MovementDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Cancelling would reduce stock by 10 (2 - 10 = -8), which is blocked when AllowNegativeStock is false
        var cancelStockFail = await _purchaseService.CancelPurchaseAsync(draft.Data!.Id, "Stock check", _adminUser.Id);
        cancelStockFail.Success.Should().BeFalse();
        cancelStockFail.ErrorMessage.Should().Contain("below zero");

        // 4. Allowed when AllowNegativeStock is true
        var settings = await _context.ShopSettings.FirstAsync();
        settings.AllowNegativeStock = true;
        await _context.SaveChangesAsync();

        var cancelSuccess = await _purchaseService.CancelPurchaseAsync(draft.Data!.Id, "Overdrawn allowed", _adminUser.Id);
        cancelSuccess.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Test07_PurchaseReturn_Limits_StockEffect_AndCancellation()
    {
        var draft = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 10m, UnitCost = 15m } }
        }, _adminUser.Id);

        await _purchaseService.PostPurchaseAsync(draft.Data!.Id, null, null, _adminUser.Id);

        // 1. Post return of 6 items
        var retRes = await _returnService.PostReturnAsync(new CreatePurchaseReturnDto
        {
            SupplierId = _supplierA.Id,
            PurchaseId = draft.Data!.Id,
            Items = new List<CreatePurchaseReturnItemDto>
            {
                new() { ProductId = _product1.Id, Quantity = 6m, UnitCost = 15m }
            }
        }, _adminUser.Id);
        retRes.Success.Should().BeTrue();

        // Ledger reflects negative movement (-6)
        var stockAfterReturn = await _purchaseRepo.GetProductCurrentStockAsync(_product1.Id);
        stockAfterReturn.Should().Be(4m); // 10 - 6 = 4

        // 2. Second return of 5 items exceeds 10 purchased (6 + 5 = 11 > 10)
        var retExcess = await _returnService.PostReturnAsync(new CreatePurchaseReturnDto
        {
            SupplierId = _supplierA.Id,
            PurchaseId = draft.Data!.Id,
            Items = new List<CreatePurchaseReturnItemDto>
            {
                new() { ProductId = _product1.Id, Quantity = 5m, UnitCost = 15m }
            }
        }, _adminUser.Id);
        retExcess.Success.Should().BeFalse();
        retExcess.ErrorMessage.Should().Contain("Maximum returnable quantity");

        // 3. Cancel the first return
        var cancelRet = await _returnService.CancelPurchaseReturnAsync(retRes.Data!.Id, "Returned by mistake", _adminUser.Id);
        cancelRet.Success.Should().BeTrue();

        // Stock restored via PurchaseReturnCancel (+6)
        var stockAfterCancel = await _purchaseRepo.GetProductCurrentStockAsync(_product1.Id);
        stockAfterCancel.Should().Be(10m);
    }

    [Fact]
    public async Task Test08_SupplierPayments_Limits_AndPaymentStatus_IncludingOverdue()
    {
        var draft = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            DueDate = DateTime.Today.AddDays(-1), // Due yesterday -> Overdue
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 10m, UnitCost = 10m } } // GrandTotal = 100.00
        }, _adminUser.Id);

        await _purchaseService.PostPurchaseAsync(draft.Data!.Id, null, null, _adminUser.Id);

        var pDetail = await _purchaseService.GetPurchaseByIdAsync(draft.Data!.Id);
        pDetail.Data!.PaymentStatus.Should().Be(PurchasePaymentStatus.Overdue);

        // 1. Partial payment of 40
        var pay1 = await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = _supplierA.Id,
            PurchaseId = draft.Data!.Id,
            Amount = 40.00m
        }, _adminUser.Id);
        pay1.Success.Should().BeTrue();

        // 2. Overpayment attempt (40 + 70 = 110 > 100)
        var payExcess = await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = _supplierA.Id,
            PurchaseId = draft.Data!.Id,
            Amount = 70.00m
        }, _adminUser.Id);
        payExcess.Success.Should().BeFalse();
        payExcess.ErrorMessage.Should().Contain("exceeds");

        // 3. Pay remaining 60
        var pay2 = await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = _supplierA.Id,
            PurchaseId = draft.Data!.Id,
            Amount = 60.00m
        }, _adminUser.Id);
        pay2.Success.Should().BeTrue();

        var pDetailPaid = await _purchaseService.GetPurchaseByIdAsync(draft.Data!.Id);
        pDetailPaid.Data!.PaymentStatus.Should().Be(PurchasePaymentStatus.Paid);
        pDetailPaid.Data.PaidAmount.Should().Be(100.00m);
        pDetailPaid.Data.Balance.Should().Be(0.00m);
    }

    [Fact]
    public async Task Test09_SupplierBalance_Formula_AccurateCalculation()
    {
        // Opening balance for Supplier B = 200.00
        // Purchase 1 posted: +500.00
        var p1 = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierB.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 50m, UnitCost = 10m } }
        }, _adminUser.Id);
        await _purchaseService.PostPurchaseAsync(p1.Data!.Id, null, null, _adminUser.Id);

        // Purchase 2 posted: +300.00
        var p2 = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierB.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 30m, UnitCost = 10m } }
        }, _adminUser.Id);
        await _purchaseService.PostPurchaseAsync(p2.Data!.Id, null, null, _adminUser.Id);

        // Purchase return posted: -100.00
        await _returnService.PostReturnAsync(new CreatePurchaseReturnDto
        {
            SupplierId = _supplierB.Id,
            PurchaseId = p1.Data!.Id,
            Items = new List<CreatePurchaseReturnItemDto> { new() { ProductId = _product1.Id, Quantity = 10m, UnitCost = 10m } }
        }, _adminUser.Id);

        // Payment 1: -250.00
        await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = _supplierB.Id,
            Amount = 250.00m
        }, _adminUser.Id);

        // Cancelled payment: does not reduce balance
        var payCancel = await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = _supplierB.Id,
            Amount = 50.00m
        }, _adminUser.Id);
        await _paymentService.CancelPaymentAsync(payCancel.Data!.Id, "Error", _adminUser.Id);

        // Expected = 200 (Opening) + 500 (P1) + 300 (P2) - 100 (Return) - 250 (Payment) = 650.00
        var balanceRes = await _ledgerService.GetSupplierBalanceAsync(_supplierB.Id);
        balanceRes.Success.Should().BeTrue();
        balanceRes.Data.Should().Be(650.00m);
    }

    [Fact]
    public async Task Test10_SupplierLedger_ChronologicalRunningBalance_AndTotals()
    {
        var fromDate = DateTime.Today.AddDays(-10);
        var toDate = DateTime.Today.AddDays(10);

        var p = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierB.Id,
            PurchaseDate = DateTime.Today,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 10m, UnitCost = 10m } }
        }, _adminUser.Id);
        await _purchaseService.PostPurchaseAsync(p.Data!.Id, null, null, _adminUser.Id);

        var reportRes = await _ledgerService.GetSupplierLedgerReportAsync(_supplierB.Id, fromDate, toDate);
        reportRes.Success.Should().BeTrue();

        var report = reportRes.Data!;
        report.OpeningBalance.Should().Be(200.00m);
        report.Entries.Should().NotBeEmpty();
        report.ClosingBalance.Should().Be(report.OpeningBalance + report.TotalDebits - report.TotalCredits);
    }

    [Fact]
    public async Task Test11_NumberSequence_UniquenessUnderConcurrentSaves()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"test_seq_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            // Initialize database schema
            using (var initCtx = new AppDbContext(options))
            {
                await initCtx.Database.MigrateAsync();
            }

            // Run 20 concurrent tasks, each with its own DbContext instance in WAL mode
            var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
            {
                using var taskCtx = new AppDbContext(options);
                var runner = new TransactionRunner(taskCtx);
                return await runner.RunInTransactionAsync(async () =>
                {
                    var seqService = new NumberSequenceService(taskCtx);
                    return await seqService.GetNextNumberAsync("PUR");
                });
            })).ToArray();

            var numbers = await Task.WhenAll(tasks);

            numbers.Should().HaveCount(20);
            numbers.Distinct().Should().HaveCount(20, "Generated document numbers must never duplicate even with simultaneous saves across separate DbContext instances");
            numbers.All(n => n.StartsWith("PUR-")).Should().BeTrue();
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

    [Fact]
    public async Task Test12_DuplicateSupplierInvoiceNo_IsRejected()
    {
        var p1 = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            SupplierInvoiceNo = "INV-DUPLICATE-01",
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 1m, UnitCost = 10m } }
        }, _adminUser.Id);
        p1.Success.Should().BeTrue();

        // Same supplier + invoice number is rejected
        var pDuplicate = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            SupplierInvoiceNo = "INV-DUPLICATE-01",
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 1m, UnitCost = 10m } }
        }, _adminUser.Id);

        pDuplicate.Success.Should().BeFalse();
        pDuplicate.ErrorMessage.Should().Contain("already been used");

        // Different supplier + same invoice number is allowed
        var pDifferentSupplier = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierB.Id,
            SupplierInvoiceNo = "INV-DUPLICATE-01",
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 1m, UnitCost = 10m } }
        }, _adminUser.Id);

        pDifferentSupplier.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Test13_PermissionsPerRole_EnforcedAcrossServices()
    {
        // Single-user system: Permission checks are removed, all operations succeed
        var draft = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 1m, UnitCost = 10m } }
        }, _managerUser.Id);
        draft.Success.Should().BeTrue();

        var post = await _purchaseService.PostPurchaseAsync(draft.Data!.Id, null, null, _managerUser.Id);
        post.Success.Should().BeTrue();

        var pay = await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = _supplierA.Id,
            Amount = 10m
        }, _managerUser.Id);
        pay.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Test14_PostedByUserId_SetToLoggedInUser()
    {
        var draft = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 2m, UnitCost = 10m } }
        }, _managerUser.Id);

        var postRes = await _purchaseService.PostPurchaseAsync(draft.Data!.Id, null, null, _managerUser.Id);
        postRes.Success.Should().BeTrue();

        var purchase = await _context.Purchases.FindAsync(draft.Data!.Id);
        purchase!.PostedByUserId.Should().Be(_managerUser.Id);
    }

    [Fact]
    public async Task Test15_CostDifferenceWarning_WarnsWhenCostDiffersByMoreThan30Percent()
    {
        // _product1 has CostPrice = 10.00
        // UnitCost = 15.00 (+50% difference -> should trigger warning list)
        var draft = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = _supplierA.Id,
            Items = new List<CreatePurchaseItemDto>
            {
                new() { ProductId = _product1.Id, Quantity = 5m, UnitCost = 15.00m }
            }
        }, _adminUser.Id);

        draft.Success.Should().BeTrue();
        draft.Data!.Warnings.Should().NotBeEmpty();
        draft.Data.Warnings.Any(w => w.Contains("differs by")).Should().BeTrue();
    }

    [Fact]
    public async Task Test16_Migration_Phase2ToPhase3_PreservesAllExistingDataWithoutLoss()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"test_migration_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            // 1. Apply migrations ONLY up to Phase2_MasterData
            using (var ctx = new AppDbContext(options))
            {
                var migrator = ctx.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20261030003411_Phase2_MasterData");

                // 2. Insert sample role, user, supplier, category, unit, product, stock movement, audit log
                var adminRole = await ctx.Roles.FirstOrDefaultAsync(r => r.Name == RoleDefaults.Admin)
                    ?? new Role { Name = RoleDefaults.Admin };
                if (adminRole.Id == 0) ctx.Roles.Add(adminRole);

                var user = new User
                {
                    Username = "seedadmin",
                    PasswordHash = "fakehash",
                    FullName = "Seed Admin",
                    Role = adminRole,
                    IsActive = true,
                    MustChangePassword = false
                };
                ctx.Users.Add(user);

                var supplier = new Supplier
                {
                    Name = "Sample PreMigration Supplier",
                    Phone = "123-456-7890",
                    Email = "premigration@supplier.com",
                    IsActive = true,
                    OpeningBalance = 350.00m
                };
                ctx.Suppliers.Add(supplier);

                var category = new Category { Name = "PreMigration Category", IsActive = true };
                var unit = new Unit { Name = "Pieces", ShortName = "pcs", IsActive = true };
                ctx.Categories.Add(category);
                ctx.Units.Add(unit);

                var product = new Product
                {
                    Name = "PreMigration Product",
                    Sku = "PRE-001",
                    Barcode = "999000111222",
                    CostPrice = 12.50m,
                    SalePrice = 25.00m,
                    Category = category,
                    Unit = unit,
                    IsActive = true
                };
                ctx.Products.Add(product);

                var movement = new StockMovement
                {
                    Product = product,
                    MovementType = StockMovementType.Opening,
                    Quantity = 50m,
                    UnitCost = 12.50m,
                    ReferenceType = "Opening",
                    ReferenceId = 1,
                    MovementDate = DateTime.UtcNow,
                    Note = "Opening stock before migration"
                };
                ctx.StockMovements.Add(movement);

                var audit = new AuditLog
                {
                    User = user,
                    Action = "PreMigrationSeed",
                    EntityName = "Product",
                    EntityId = 1,
                    Details = "Pre-migration audit row verification",
                    Timestamp = DateTime.UtcNow
                };
                ctx.AuditLogs.Add(audit);

                await ctx.SaveChangesAsync();
            }

            // 3. Now apply Phase3_Purchases (all pending migrations)
            using (var ctx = new AppDbContext(options))
            {
                await ctx.Database.MigrateAsync();
            }

            // 4. Assert every pre-migration row is still present with unchanged data
            using (var verifyCtx = new AppDbContext(options))
            {
                var user = await verifyCtx.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == "seedadmin");
                user.Should().NotBeNull();
                user!.FullName.Should().Be("Seed Admin");
                user.Role!.Name.Should().Be(RoleDefaults.Admin);

                var supplier = await verifyCtx.Suppliers.FirstOrDefaultAsync(s => s.Name == "Sample PreMigration Supplier");
                supplier.Should().NotBeNull();
                supplier!.Phone.Should().Be("123-456-7890");
                supplier.OpeningBalance.Should().Be(350.00m);

                var product = await verifyCtx.Products.Include(p => p.Category).Include(p => p.Unit).FirstOrDefaultAsync(p => p.Sku == "PRE-001");
                product.Should().NotBeNull();
                product!.Name.Should().Be("PreMigration Product");
                product.CostPrice.Should().Be(12.50m);
                product.SalePrice.Should().Be(25.00m);
                product.Category!.Name.Should().Be("PreMigration Category");
                product.Unit!.Name.Should().Be("Pieces");

                var movements = await verifyCtx.StockMovements.Where(m => m.ProductId == product.Id).ToListAsync();
                movements.Should().HaveCount(1);
                movements[0].MovementType.Should().Be(StockMovementType.Opening);
                movements[0].Quantity.Should().Be(50m);
                movements[0].UnitCost.Should().Be(12.50m);

                var audit = await verifyCtx.AuditLogs.FirstOrDefaultAsync(a => a.Action == "PreMigrationSeed");
                audit.Should().NotBeNull();
                audit!.Details.Should().Be("Pre-migration audit row verification");

                // Verify Phase 3 new tables exist and are functional
                var purchasesCount = await verifyCtx.Purchases.CountAsync();
                purchasesCount.Should().Be(0);
                var paymentsCount = await verifyCtx.SupplierPayments.CountAsync();
                paymentsCount.Should().Be(0);
                var returnsCount = await verifyCtx.PurchaseReturns.CountAsync();
                returnsCount.Should().Be(0);
            }
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

    [Fact]
    public async Task Test17_PostPurchase_ParallelPostsForSameProduct_ProducesCorrectStockAndWeightedAverageCost()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"test_parallel_post_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            int supplierId;
            int productId;
            int adminUserId;

            // Setup database and seed data
            using (var initCtx = new AppDbContext(options))
            {
                await initCtx.Database.MigrateAsync();

                var role = await initCtx.Roles.FirstOrDefaultAsync(r => r.Name == RoleDefaults.Admin)
                    ?? new Role { Name = RoleDefaults.Admin };
                if (role.Id == 0) initCtx.Roles.Add(role);

                var admin = new User { Username = "admin1", PasswordHash = "hash", FullName = "Admin", Role = role, IsActive = true };
                initCtx.Users.Add(admin);

                var supplier = new Supplier { Name = "Parallel Supplier", Phone = "555-1234", IsActive = true };
                initCtx.Suppliers.Add(supplier);

                var cat = new Category { Name = "General", IsActive = true };
                var unit = new Unit { Name = "Pieces", ShortName = "pcs", IsActive = true };
                initCtx.Categories.Add(cat);
                initCtx.Units.Add(unit);

                // Initial product: Stock = 0, CostPrice = 10.00
                var product = new Product
                {
                    Name = "Parallel Test Product",
                    Sku = "PAR-001",
                    CostPrice = 10.00m,
                    SalePrice = 50.00m,
                    Category = cat,
                    Unit = unit,
                    IsActive = true
                };
                initCtx.Products.Add(product);
                await initCtx.SaveChangesAsync();

                supplierId = supplier.Id;
                productId = product.Id;
                adminUserId = admin.Id;
            }

            // Create two draft purchases in advance
            int draft1Id;
            int draft2Id;
            using (var draftCtx = new AppDbContext(options))
            {
                var runner = new TransactionRunner(draftCtx);
                var pRepo = new PurchaseRepository(draftCtx);
                var sRepo = new SupplierRepository(draftCtx);
                var prRepo = new ProductRepository(draftCtx);
                var ssRepo = new ShopSettingRepository(draftCtx);
                var seq = new NumberSequenceService(draftCtx);
                var audit = new Mock<IAuditLogService>().Object;
                var pdf = new Mock<IPdfReportService>().Object;
                

                var pService = new PurchaseService(runner, pRepo, sRepo, prRepo, ssRepo, seq, audit, pdf,
                    new ShopPOS.Core.Validators.CreatePurchaseValidator(),
                    new ShopPOS.Core.Validators.UpdatePurchaseValidator());

                // Draft 1: 10 units @ 20.00
                var d1 = await pService.CreatePurchaseDraftAsync(new CreatePurchaseDto
                {
                    SupplierId = supplierId,
                    Items = new List<CreatePurchaseItemDto> { new() { ProductId = productId, Quantity = 10m, UnitCost = 20.00m } }
                }, adminUserId);
                d1.Success.Should().BeTrue();
                draft1Id = d1.Data!.Id;

                // Draft 2: 10 units @ 40.00
                var d2 = await pService.CreatePurchaseDraftAsync(new CreatePurchaseDto
                {
                    SupplierId = supplierId,
                    Items = new List<CreatePurchaseItemDto> { new() { ProductId = productId, Quantity = 10m, UnitCost = 40.00m } }
                }, adminUserId);
                d2.Success.Should().BeTrue();
                draft2Id = d2.Data!.Id;
            }

            // Run PostPurchaseAsync on both drafts simultaneously in parallel tasks using separate DbContext instances
            var task1 = Task.Run(async () =>
            {
                using var ctx1 = new AppDbContext(options);
                var runner1 = new TransactionRunner(ctx1);
                var pRepo1 = new PurchaseRepository(ctx1);
                var sRepo1 = new SupplierRepository(ctx1);
                var prRepo1 = new ProductRepository(ctx1);
                var ssRepo1 = new ShopSettingRepository(ctx1);
                var seq1 = new NumberSequenceService(ctx1);
                var audit1 = new Mock<IAuditLogService>().Object;
                var pdf1 = new Mock<IPdfReportService>().Object;
                

                var pService1 = new PurchaseService(runner1, pRepo1, sRepo1, prRepo1, ssRepo1, seq1, audit1, pdf1,
                    new ShopPOS.Core.Validators.CreatePurchaseValidator(),
                    new ShopPOS.Core.Validators.UpdatePurchaseValidator());

                return await pService1.PostPurchaseAsync(draft1Id, null, null, adminUserId);
            });

            var task2 = Task.Run(async () =>
            {
                using var ctx2 = new AppDbContext(options);
                var runner2 = new TransactionRunner(ctx2);
                var pRepo2 = new PurchaseRepository(ctx2);
                var sRepo2 = new SupplierRepository(ctx2);
                var prRepo2 = new ProductRepository(ctx2);
                var ssRepo2 = new ShopSettingRepository(ctx2);
                var seq2 = new NumberSequenceService(ctx2);
                var audit2 = new Mock<IAuditLogService>().Object;
                var pdf2 = new Mock<IPdfReportService>().Object;
                

                var pService2 = new PurchaseService(runner2, pRepo2, sRepo2, prRepo2, ssRepo2, seq2, audit2, pdf2,
                    new ShopPOS.Core.Validators.CreatePurchaseValidator(),
                    new ShopPOS.Core.Validators.UpdatePurchaseValidator());

                return await pService2.PostPurchaseAsync(draft2Id, null, null, adminUserId);
            });

            var results = await Task.WhenAll(task1, task2);
            results[0].Success.Should().BeTrue();
            results[1].Success.Should().BeTrue();

            // Verify final state from a fresh DbContext
            using (var verifyCtx = new AppDbContext(options))
            {
                var pRepo = new PurchaseRepository(verifyCtx);
                decimal finalStock = await pRepo.GetProductCurrentStockAsync(productId);
                finalStock.Should().Be(20.00m, "Two purchases of 10 units each must yield 20 units total");

                var product = await verifyCtx.Products.FindAsync(productId);
                // Weighted average: (10 * 20 + 10 * 40) / 20 = 600 / 20 = 30.00
                product!.CostPrice.Should().Be(30.00m, "Parallel post should produce the correct final weighted average cost");

                var movements = await verifyCtx.StockMovements.Where(m => m.ProductId == productId).ToListAsync();
                movements.Should().HaveCount(2);

                var p1 = await verifyCtx.Purchases.FindAsync(draft1Id);
                var p2 = await verifyCtx.Purchases.FindAsync(draft2Id);
                p1!.Status.Should().Be(PurchaseStatus.Posted);
                p2!.Status.Should().Be(PurchaseStatus.Posted);
            }
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

    [Fact]
    public async Task Test18_Consistency_SequenceOfPostsPaymentsCancellationsAndReturns_MaintainsFullIntegrity()
    {
        // 1. Setup a fresh supplier with opening balance 500.00
        var supplier = new Supplier
        {
            Name = "Consistency Supplier",
            Phone = "123-4567",
            IsActive = true,
            OpeningBalance = 500.00m
        };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        // 2. Post Purchase 1: Grand total = 1000.00 (100 units @ 10)
        var p1Draft = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = supplier.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 100m, UnitCost = 10.00m } }
        }, _adminUser.Id);
        p1Draft.Success.Should().BeTrue();
        await _purchaseService.PostPurchaseAsync(p1Draft.Data!.Id, null, null, _adminUser.Id);

        // 3. Post Purchase 2: Grand total = 600.00 (60 units @ 10)
        var p2Draft = await _purchaseService.CreatePurchaseDraftAsync(new CreatePurchaseDto
        {
            SupplierId = supplier.Id,
            Items = new List<CreatePurchaseItemDto> { new() { ProductId = _product1.Id, Quantity = 60m, UnitCost = 10.00m } }
        }, _adminUser.Id);
        p2Draft.Success.Should().BeTrue();
        await _purchaseService.PostPurchaseAsync(p2Draft.Data!.Id, null, null, _adminUser.Id);

        // 4. Record Payment 1 for Purchase 1: 400.00
        var pay1 = await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = supplier.Id,
            PurchaseId = p1Draft.Data!.Id,
            Amount = 400.00m
        }, _adminUser.Id);
        pay1.Success.Should().BeTrue();

        // 5. Record Payment 2 for Purchase 1: 300.00
        var pay2 = await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = supplier.Id,
            PurchaseId = p1Draft.Data!.Id,
            Amount = 300.00m
        }, _adminUser.Id);
        pay2.Success.Should().BeTrue();

        // 6. Cancel Payment 2 (300.00)
        var cancelPay2 = await _paymentService.CancelPaymentAsync(pay2.Data!.Id, "Accidental duplicate entry", _adminUser.Id);
        cancelPay2.Success.Should().BeTrue();

        // 7. Post Purchase Return on Purchase 1: 15 units @ 10.00 = 150.00
        var ret1 = await _returnService.PostReturnAsync(new CreatePurchaseReturnDto
        {
            SupplierId = supplier.Id,
            PurchaseId = p1Draft.Data!.Id,
            Items = new List<CreatePurchaseReturnItemDto> { new() { ProductId = _product1.Id, Quantity = 15m, UnitCost = 10.00m } }
        }, _adminUser.Id);
        ret1.Success.Should().BeTrue();

        // 8. Record Payment 3 for Purchase 2: 250.00
        var pay3 = await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = supplier.Id,
            PurchaseId = p2Draft.Data!.Id,
            Amount = 250.00m
        }, _adminUser.Id);
        pay3.Success.Should().BeTrue();

        // 9. Record unallocated Payment 4: 100.00
        var pay4 = await _paymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
        {
            SupplierId = supplier.Id,
            PurchaseId = null,
            Amount = 100.00m
        }, _adminUser.Id);
        pay4.Success.Should().BeTrue();

        // Assert 1: For each purchase, Purchase.PaidAmount equals the sum of its non-cancelled payments
        var p1Db = await _context.Purchases.FindAsync(p1Draft.Data!.Id);
        var p1PaymentsSum = await _context.SupplierPayments
            .Where(p => p.PurchaseId == p1Db!.Id && !p.IsCancelled)
            .SumAsync(p => p.Amount);
        p1Db!.PaidAmount.Should().Be(400.00m);
        p1Db.PaidAmount.Should().Be(p1PaymentsSum);

        var p2Db = await _context.Purchases.FindAsync(p2Draft.Data!.Id);
        var p2PaymentsSum = await _context.SupplierPayments
            .Where(p => p.PurchaseId == p2Db!.Id && !p.IsCancelled)
            .SumAsync(p => p.Amount);
        p2Db!.PaidAmount.Should().Be(250.00m);
        p2Db.PaidAmount.Should().Be(p2PaymentsSum);

        // Assert 2: Supplier balance from GetSupplierBalanceAsync equals expected math:
        // Opening (500) + P1 (1000) + P2 (600) - Return (150) - Active Payments (400 + 250 + 100) = 1200.00
        decimal expectedBalance = 500.00m + 1000.00m + 600.00m - 150.00m - 750.00m; // 1200.00m
        var balanceRes = await _ledgerService.GetSupplierBalanceAsync(supplier.Id);
        balanceRes.Success.Should().BeTrue();
        balanceRes.Data.Should().Be(expectedBalance);

        // Assert 3: Supplier balance from GetSupplierLedgerReportAsync ClosingBalance matches GetSupplierBalanceAsync exactly
        var ledgerRes = await _ledgerService.GetSupplierLedgerReportAsync(supplier.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
        ledgerRes.Success.Should().BeTrue();
        ledgerRes.Data!.ClosingBalance.Should().Be(balanceRes.Data);
        ledgerRes.Data.ClosingBalance.Should().Be(expectedBalance);
    }

    [Fact]
    public async Task Test19_Migration_RemoveStockCount_PreservesProductsPurchasesSuppliersAndStockMovements()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"test_migration_rmcount_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            // 1. Apply migrations up to Phase4_Inventory
            using (var ctx = new AppDbContext(options))
            {
                var migrator = ctx.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20261031033632_Phase4_Inventory");

                var role = new Role { Name = "Owner" };
                ctx.Roles.Add(role);

                var user = new User
                {
                    Username = "owner",
                    FullName = "Owner",
                    Role = role,
                    IsActive = true
                };
                ctx.Users.Add(user);

                var supplier = new Supplier
                {
                    Name = "Migration Test Supplier",
                    Phone = "123-555-7890",
                    IsActive = true
                };
                ctx.Suppliers.Add(supplier);

                var cat = new Category { Name = "Migration Test Category", IsActive = true };
                var unit = new Unit { Name = "Piece", ShortName = "pc", IsActive = true };
                ctx.Categories.Add(cat);
                ctx.Units.Add(unit);

                var product = new Product
                {
                    Name = "Migration Test Product",
                    Sku = "MIG-001",
                    CostPrice = 20m,
                    SalePrice = 35m,
                    TrackStock = true,
                    Category = cat,
                    Unit = unit,
                    IsActive = true
                };
                ctx.Products.Add(product);

                var purchase = new Purchase
                {
                    PurchaseNumber = "PUR-MIG-001",
                    Supplier = supplier,
                    PurchaseDate = DateTime.Now,
                    SubTotal = 200m,
                    GrandTotal = 200m,
                    Status = PurchaseStatus.Posted,
                    PaidAmount = 200m
                };
                purchase.Items.Add(new PurchaseItem
                {
                    Product = product,
                    Quantity = 10m,
                    UnitCost = 20m,
                    LineTotal = 200m
                });
                ctx.Purchases.Add(purchase);

                var mov = new StockMovement
                {
                    Product = product,
                    MovementType = StockMovementType.Purchase,
                    Quantity = 10m,
                    UnitCost = 20m,
                    MovementDate = DateTime.Now,
                    Note = "Pre-migration stock"
                };
                ctx.StockMovements.Add(mov);

                await ctx.SaveChangesAsync();
            }

            // 2. Apply pending migration (RemoveStockCount)
            using (var ctx = new AppDbContext(options))
            {
                await ctx.Database.MigrateAsync();
            }

            // 3. Verify all data is preserved
            using (var verifyCtx = new AppDbContext(options))
            {
                var prod = await verifyCtx.Products.FirstOrDefaultAsync(p => p.Sku == "MIG-001");
                prod.Should().NotBeNull();
                prod!.CostPrice.Should().Be(20m);

                var supp = await verifyCtx.Suppliers.FirstOrDefaultAsync(s => s.Name == "Migration Test Supplier");
                supp.Should().NotBeNull();

                var purch = await verifyCtx.Purchases.Include(p => p.Items).FirstOrDefaultAsync(p => p.PurchaseNumber == "PUR-MIG-001");
                purch.Should().NotBeNull();
                purch!.Items.Should().HaveCount(1);
                purch.GrandTotal.Should().Be(200m);

                var movements = await verifyCtx.StockMovements.Where(m => m.ProductId == prod.Id).ToListAsync();
                movements.Should().HaveCount(1);
                movements[0].Quantity.Should().Be(10m);
            }
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
