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
using ShopPOS.Data.Seeding;
using ShopPOS.Data.Services;

namespace ShopPOS.Tests;

public class SalesModuleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    private readonly SaleCalculator _calculator;
    private readonly ProductRepository _productRepo;
    private readonly CustomerRepository _customerRepo;
    private readonly Repository<StockMovement> _movementRepo;
    private readonly Repository<ProductBarcode> _barcodeRepo;
    private readonly ShopSettingRepository _settingRepo;
    private readonly ShopSettingService _settingService;
    private readonly StockLedgerQuery _stockLedgerQuery;
    private readonly CustomerBalanceQuery _customerBalanceQuery;
    private readonly NumberSequenceService _sequenceService;
    private readonly TransactionRunner _transactionRunner;
    private readonly Mock<IAuditLogService> _auditMock;
    private readonly CurrentUserProvider _currentUserProvider;

    private readonly SaleRepository _saleRepo;
    private readonly SaleReturnRepository _returnRepo;
    private readonly ShiftRepository _shiftRepo;

    private readonly ProductLookupService _lookupService;
    private readonly ShiftService _shiftService;
    private readonly SaleReturnService _returnService;
    private readonly SaleService _saleService;

    private Category _category = null!;
    private Unit _pcsUnit = null!;
    private Unit _kgUnit = null!;
    private Customer _customer = null!;
    private PriceLevel _wholesaleLevel = null!;
    private Product _trackProd = null!;
    private Product _nonTrackProd = null!;

    public SalesModuleTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _context = new AppDbContext(options);
        _context.Database.Migrate();

        _calculator = new SaleCalculator();
        _productRepo = new ProductRepository(_context);
        _customerRepo = new CustomerRepository(_context);
        _movementRepo = new Repository<StockMovement>(_context);
        _barcodeRepo = new Repository<ProductBarcode>(_context);
        _settingRepo = new ShopSettingRepository(_context);
        _settingService = new ShopSettingService(_settingRepo);
        _stockLedgerQuery = new StockLedgerQuery(_context);
        _customerBalanceQuery = new CustomerBalanceQuery(_context);
        _sequenceService = new NumberSequenceService(_context);
        _transactionRunner = new TransactionRunner(_context);
        _auditMock = new Mock<IAuditLogService>();
        _currentUserProvider = new CurrentUserProvider();

        _saleRepo = new SaleRepository(_context);
        _returnRepo = new SaleReturnRepository(_context);
        _shiftRepo = new ShiftRepository(_context);

        _lookupService = new ProductLookupService(_productRepo);
        _shiftService = new ShiftService(
            _shiftRepo,
            _sequenceService,
            _auditMock.Object,
            _currentUserProvider,
            new OpenShiftValidator(),
            new CloseShiftValidator(),
            new CashMovementValidator());

        _returnService = new SaleReturnService(
            _returnRepo,
            _saleRepo,
            _productRepo,
            _customerRepo,
            _movementRepo,
            _stockLedgerQuery,
            _customerBalanceQuery,
            _settingService,
            _shiftRepo,
            _sequenceService,
            _transactionRunner,
            _auditMock.Object,
            _currentUserProvider,
            _calculator,
            new CreateSaleReturnValidator());

        _saleService = new SaleService(
            _saleRepo,
            _productRepo,
            _customerRepo,
            _movementRepo,
            _stockLedgerQuery,
            _customerBalanceQuery,
            _settingService,
            _shiftRepo,
            _sequenceService,
            _transactionRunner,
            _auditMock.Object,
            _currentUserProvider,
            _calculator,
            new SaveSaleValidator(),
            new CompleteSaleValidator());

        SeedBaseData();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void SeedBaseData()
    {
        var seeder = new DataSeeder(_context);
        seeder.SeedAsync().GetAwaiter().GetResult();

        _category = _context.Categories.First();
        _pcsUnit = _context.Units.First(u => u.ShortName == "pcs");
        _kgUnit = _context.Units.First(u => u.ShortName == "kg");
        _customer = _context.Customers.First();
        _wholesaleLevel = _context.PriceLevels.First(p => p.Name == "Wholesale");

        _trackProd = new Product
        {
            Name = "Tracked Energy Bar",
            Sku = "BAR-001",
            Barcode = "1112223334445",
            CategoryId = _category.Id,
            UnitId = _pcsUnit.Id,
            CostPrice = 1.00m,
            SalePrice = 2.50m,
            MinSalePrice = 2.00m,
            TaxPercent = 10.0m,
            TrackStock = true,
            AllowDecimalQuantity = false,
            IsActive = true
        };
        _context.Products.Add(_trackProd);

        _nonTrackProd = new Product
        {
            Name = "Gift Wrap Service",
            Sku = "SRV-001",
            Barcode = "9998887776665",
            CategoryId = _category.Id,
            UnitId = _pcsUnit.Id,
            CostPrice = 0.50m,
            SalePrice = 3.00m,
            MinSalePrice = 2.50m,
            TaxPercent = 0.0m,
            TrackStock = false,
            AllowDecimalQuantity = false,
            IsActive = true
        };
        _context.Products.Add(_nonTrackProd);
        _context.SaveChanges();

        // Opening stock for tracked item = 20 units
        _context.StockMovements.Add(new StockMovement
        {
            ProductId = _trackProd.Id,
            MovementType = StockMovementType.Opening,
            Quantity = 20m,
            UnitCost = _trackProd.CostPrice,
            MovementDate = DateTime.Now
        });
        _context.SaveChanges();
    }

    [Fact]
    public void SaleCalculator_CalculatesLineAndBillDiscounts_ProportionallyWithNoLoss()
    {
        // Line 1: 2 x 10 = 20, 10% line discount -> net 18, tax 10%
        // Line 2: 1 x 30 = 30, $5 fixed line discount -> net 25, tax 10%
        // Total net before bill discount = 43. Bill discount = $10 fixed.
        var lines = new List<SaleLineInput>
        {
            new() { Quantity = 2m, UnitPrice = 10m, DiscountPercent = 10m, TaxPercent = 10m },
            new() { Quantity = 1m, UnitPrice = 30m, DiscountAmount = 5m, TaxPercent = 10m }
        };

        var result = _calculator.CalculateSale(lines, billDiscountAmount: 10m, pricesIncludeTax: false);

        result.SubTotal.Should().Be(50m);
        result.LineDiscountTotal.Should().Be(7m); // 2 + 5
        result.BillDiscountAmount.Should().Be(10m);

        // Sum of allocated bill discounts must equal exact total bill discount
        var sumAllocated = result.Lines.Sum(l => l.BillDiscountAllocated);
        sumAllocated.Should().Be(10m);

        // All net amounts after discounts should sum to 50 - 7 - 10 = 33
        result.Lines.Sum(l => l.NetAfterAllDiscounts).Should().Be(33m);
    }

    [Fact]
    public void SaleCalculator_PricesIncludeTax_Mode1_ExtractsTaxProperly()
    {
        // 1 item @ $110, tax 10%, prices include tax
        // Net of tax = 110 / 1.10 = 100, Tax = 10, Grand Total = 110
        var lines = new List<SaleLineInput>
        {
            new() { Quantity = 1m, UnitPrice = 110m, TaxPercent = 10m }
        };

        var result = _calculator.CalculateSale(lines, pricesIncludeTax: true);

        result.SubTotal.Should().Be(110m);
        result.TaxAmount.Should().Be(10m);
        result.GrandTotal.Should().Be(110m);
        result.Lines[0].NetOfTax.Should().Be(100m);
    }

    [Fact]
    public void SaleCalculator_PricesExcludeTax_Mode2_AddsTaxProperly()
    {
        // 1 item @ $100, tax 10%, prices exclude tax
        // Net = 100, Tax = 10, Grand Total = 110
        var lines = new List<SaleLineInput>
        {
            new() { Quantity = 1m, UnitPrice = 100m, TaxPercent = 10m }
        };

        var result = _calculator.CalculateSale(lines, pricesIncludeTax: false);

        result.SubTotal.Should().Be(100m);
        result.TaxAmount.Should().Be(10m);
        result.GrandTotal.Should().Be(110m);
    }

    [Theory]
    [InlineData(10.23, 0.05, 10.25, 0.02)]
    [InlineData(10.22, 0.05, 10.20, -0.02)]
    [InlineData(17.40, 1.00, 17.00, -0.40)]
    [InlineData(17.60, 1.00, 18.00, 0.40)]
    [InlineData(22.00, 5.00, 20.00, -2.00)]
    [InlineData(23.00, 5.00, 25.00, 2.00)]
    [InlineData(15.75, 0.00, 15.75, 0.00)]
    public void SaleCalculator_CashRounding_RoundsHalfAwayFromZeroAndStoresDifference(
        decimal grandTotalPreRound,
        decimal step,
        decimal expectedGrandTotal,
        decimal expectedRoundOff)
    {
        var lines = new List<SaleLineInput>
        {
            new() { Quantity = 1m, UnitPrice = grandTotalPreRound, TaxPercent = 0m }
        };

        var result = _calculator.CalculateSale(lines, cashRoundingStep: step, pricesIncludeTax: false);

        result.GrandTotal.Should().Be(expectedGrandTotal);
        result.RoundOff.Should().Be(expectedRoundOff);
    }

    [Fact]
    public void SaleCalculator_Discounts_NeverMakeLineOrGrandTotalNegative()
    {
        var lines = new List<SaleLineInput>
        {
            new() { Quantity = 1m, UnitPrice = 10m, DiscountAmount = 25m, TaxPercent = 5m }
        };

        var result = _calculator.CalculateSale(lines, billDiscountAmount: 50m, pricesIncludeTax: false);

        result.Lines[0].LineTotal.Should().Be(0m);
        result.GrandTotal.Should().Be(0m);
        result.DueAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ProductLookup_ResolvesCustomerPriceLevelTier_OrFallsBackToSalePrice()
    {
        // Add tier price for wholesale level
        _context.ProductPrices.Add(new ProductPrice
        {
            ProductId = _trackProd.Id,
            PriceLevelId = _wholesaleLevel.Id,
            Price = 1.80m
        });
        await _context.SaveChangesAsync();

        // 1. Without price level -> returns standard SalePrice (2.50)
        var lookupStandard = await _lookupService.LookupProductAsync(_trackProd.Barcode!);
        lookupStandard.Found.Should().BeTrue();
        lookupStandard.ResolvedUnitPrice.Should().Be(2.50m);

        // 2. With wholesale level -> returns wholesale tier price (1.80)
        var lookupWholesale = await _lookupService.LookupProductAsync(_trackProd.Barcode!, _wholesaleLevel.Id);
        lookupWholesale.Found.Should().BeTrue();
        lookupWholesale.ResolvedUnitPrice.Should().Be(1.80m);
    }

    [Fact]
    public async Task CompleteSale_MinPriceRule_BlocksWhenDisallowed_AllowsWhenAllowed()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });

        // MinSalePrice on _trackProd is 2.00. Attempt selling at 1.50
        var dto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 1m, UnitPrice = 1.50m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 1.65m }
            }
        };

        // 1. Setting: AllowSellingBelowMinPrice = false -> rejected
        var setting = await _settingService.GetSettingsAsync();
        setting.AllowSellingBelowMinPrice = false;
        await _settingService.UpdateSettingsAsync(setting);

        var failResult = await _saleService.CompleteSaleAsync(dto);
        failResult.Success.Should().BeFalse();
        failResult.ErrorMessage.Should().Contain("below the minimum allowed price");

        // 2. Setting: AllowSellingBelowMinPrice = true -> accepted
        setting.AllowSellingBelowMinPrice = true;
        await _settingService.UpdateSettingsAsync(setting);

        var okResult = await _saleService.CompleteSaleAsync(dto);
        okResult.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteSale_DecimalQuantityRule_RejectsFractionalForNonDecimals()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });

        // _trackProd has AllowDecimalQuantity = false
        var dto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 1.5m, UnitPrice = 2.50m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 10m }
            }
        };

        var result = await _saleService.CompleteSaleAsync(dto);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not allow fractional quantities");
    }

    [Fact]
    public async Task CompleteSale_CreatesSnapshots_Movements_Number_Shift_AndAuditLog()
    {
        var shiftRes = await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });
        int shiftId = shiftRes.Data!.Id;

        decimal initialStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);

        var dto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 2m, UnitPrice = 2.50m, TaxPercent = 10m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 5.50m }
            }
        };

        var result = await _saleService.CompleteSaleAsync(dto);
        result.Success.Should().BeTrue();
        var sale = result.Data!;

        sale.SaleNumber.Should().StartWith("INV-");
        sale.Status.Should().Be(SaleStatus.Completed);
        sale.ShiftId.Should().Be(shiftId);
        sale.GrandTotal.Should().Be(5.50m);
        sale.Items.Should().HaveCount(1);
        sale.Items[0].CostPriceSnapshot.Should().Be(_trackProd.CostPrice);
        sale.Items[0].ProductName.Should().Be(_trackProd.Name);

        // Stock movement check
        decimal newStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);
        newStock.Should().Be(initialStock - 2m);

        var mov = await _context.StockMovements.FirstOrDefaultAsync(m => m.ReferenceType == "Sale" && m.ReferenceId == sale.Id);
        mov.Should().NotBeNull();
        mov!.Quantity.Should().Be(-2m);
        mov.UnitCost.Should().Be(_trackProd.CostPrice);
        mov.MovementType.Should().Be(StockMovementType.Sale);

        // Audit log check
        _auditMock.Verify(a => a.LogAsync(
            1,
            "CompleteSale",
            nameof(Sale),
            sale.Id,
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CompleteSale_Atomicity_OnFailure_RollsBackEverything()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });
        decimal initialStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);

        // Intentionally invalid: Overpayment on Card (which is disallowed)
        var dto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 2m, UnitPrice = 2.50m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Card, Amount = 100m } // Overpayment by card disallowed
            }
        };

        var result = await _saleService.CompleteSaleAsync(dto);
        result.Success.Should().BeFalse();

        // Prove no sale was created
        var salesCount = await _context.Sales.CountAsync();
        salesCount.Should().Be(0);

        // Prove no stock movements were written
        decimal currentStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);
        currentStock.Should().Be(initialStock);
    }

    [Fact]
    public async Task CompleteSale_NegativeStock_BlockedWhenSettingFalse_AllowedWhenSettingTrue()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });
        decimal currentStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id); // 20 units

        // Try to sell 25 units (exceeds 20)
        var dto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 25m, UnitPrice = 2.50m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 100m }
            }
        };

        // 1. Setting: AllowNegativeStock = false -> blocked with clear message
        var setting = await _settingService.GetSettingsAsync();
        setting.AllowNegativeStock = false;
        await _settingService.UpdateSettingsAsync(setting);

        var failResult = await _saleService.CompleteSaleAsync(dto);
        failResult.Success.Should().BeFalse();
        failResult.ErrorMessage.Should().Contain("Insufficient stock for 'Tracked Energy Bar'");

        // 2. Setting: AllowNegativeStock = true -> allowed
        setting.AllowNegativeStock = true;
        await _settingService.UpdateSettingsAsync(setting);

        var okResult = await _saleService.CompleteSaleAsync(dto);
        okResult.Success.Should().BeTrue();

        decimal finalStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);
        finalStock.Should().Be(-5m);
    }

    [Fact]
    public async Task CompleteSale_NonTrackedProducts_SkipCheckAndCreateNoStockMovement()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });

        var dto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _nonTrackProd.Id, Quantity = 10m, UnitPrice = 3.00m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 30m }
            }
        };

        var result = await _saleService.CompleteSaleAsync(dto);
        result.Success.Should().BeTrue();

        // Prove no stock movements created for non-tracked item
        var movements = await _context.StockMovements.Where(m => m.ProductId == _nonTrackProd.Id).ToListAsync();
        movements.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteSale_CreditSale_ValidatesCustomer_AllowCreditSetting_AndCreditLimit()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });

        // Customer opening balance is 45, credit limit is 500
        _customer.CreditLimit = 100m;
        _customer.OpeningBalance = 40m;
        await _customerRepo.UpdateAsync(_customer);

        // 1. Attempt credit sale without selecting customer -> fails
        var walkInCredit = new CompleteSaleDto
        {
            CustomerId = null, // walk-in
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 1m, UnitPrice = 2.50m }
            },
            Payments = new List<SalePaymentDto>() // 0 paid -> full due
        };
        var res1 = await _saleService.CompleteSaleAsync(walkInCredit);
        res1.Success.Should().BeFalse();
        res1.ErrorMessage.Should().Contain("customer must be selected");

        // 2. Setting: AllowCreditSales = false -> fails
        var setting = await _settingService.GetSettingsAsync();
        setting.AllowCreditSales = false;
        await _settingService.UpdateSettingsAsync(setting);

        var customerCredit = new CompleteSaleDto
        {
            CustomerId = _customer.Id,
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 1m, UnitPrice = 2.50m }
            },
            Payments = new List<SalePaymentDto>()
        };
        var res2 = await _saleService.CompleteSaleAsync(customerCredit);
        res2.Success.Should().BeFalse();
        res2.ErrorMessage.Should().Contain("not permitted by store settings");

        // 3. Exceeding credit limit (Limit = 100, current = 40, attempting new due = 70 -> total 110 > 100) -> fails
        setting.AllowCreditSales = true;
        await _settingService.UpdateSettingsAsync(setting);

        var overLimitCredit = new CompleteSaleDto
        {
            CustomerId = _customer.Id,
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 28m, UnitPrice = 2.50m } // 28 * 2.50 = 70
            },
            Payments = new List<SalePaymentDto>()
        };
        // Allow negative stock temporarily for this test
        setting.AllowNegativeStock = true;
        await _settingService.UpdateSettingsAsync(setting);

        var res3 = await _saleService.CompleteSaleAsync(overLimitCredit);
        res3.Success.Should().BeFalse();
        res3.ErrorMessage.Should().Contain("Credit limit of 100.00 exceeded");
    }

    [Fact]
    public async Task CompleteSale_ChangeAndOverpayment_CashAllowsChange_CardOverpaymentRejected()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });

        // Total bill = 10.00 (tax = 0)
        var dto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 4m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 20.00m, TenderedAmount = 20.00m }
            }
        };

        var result = await _saleService.CompleteSaleAsync(dto);
        result.Success.Should().BeTrue();
        var sale = result.Data!;

        sale.GrandTotal.Should().Be(10.00m);
        sale.PaidAmount.Should().Be(10.00m);
        sale.ChangeAmount.Should().Be(10.00m);
        sale.DueAmount.Should().Be(0m);

        sale.Payments[0].Amount.Should().Be(10.00m);
        sale.Payments[0].TenderedAmount.Should().Be(20.00m);
    }

    [Fact]
    public async Task HeldBill_SavesWithoutStockEffect_AndIsRemovedWhenCompleted()
    {
        decimal initialStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);

        // 1. Save held bill
        var saveDto = new SaveSaleDto
        {
            CustomerId = _customer.Id,
            Notes = "Hold for Alice",
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 3m, UnitPrice = 2.50m }
            }
        };

        var saveRes = await _saleService.SaveHeldBillAsync(saveDto);
        saveRes.Success.Should().BeTrue();
        var held = saveRes.Data!;
        held.HoldNumber.Should().StartWith("HOLD-");
        held.Status.Should().Be(SaleStatus.Held);

        // Prove no stock movements and no stock decrease
        decimal stockAfterHold = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);
        stockAfterHold.Should().Be(initialStock);

        // 2. Complete sale referencing the held bill
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });
        var completeDto = new CompleteSaleDto
        {
            HeldBillId = held.Id,
            CustomerId = _customer.Id,
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 3m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 7.50m }
            }
        };

        var compRes = await _saleService.CompleteSaleAsync(completeDto);
        compRes.Success.Should().BeTrue();

        // Prove held bill was soft-deleted
        var oldHeld = await _context.Sales.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == held.Id);
        oldHeld!.IsDeleted.Should().BeTrue();

        // Prove stock decreased only on completion
        decimal stockAfterComplete = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);
        stockAfterComplete.Should().Be(initialStock - 3m);
    }

    [Fact]
    public async Task CancelSale_CreatesReversingMovements_BlockedIfHasReturnsOrClosedShift()
    {
        var shiftRes = await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });
        int shiftId = shiftRes.Data!.Id;
        decimal stockBefore = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);

        var compRes = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 2m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 5.00m }
            }
        });
        var saleId = compRes.Data!.Id;

        // 1. Cancel with reason succeeds while shift is open
        var cancelRes = await _saleService.CancelSaleAsync(saleId, "Customer changed mind");
        cancelRes.Success.Should().BeTrue();

        // Prove reversing movement
        decimal stockAfterCancel = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);
        stockAfterCancel.Should().Be(stockBefore);

        var reversingMov = await _context.StockMovements
            .FirstOrDefaultAsync(m => m.MovementType == StockMovementType.SaleCancel && m.ReferenceId == saleId);
        reversingMov.Should().NotBeNull();
        reversingMov!.Quantity.Should().Be(2m);
    }

    [Fact]
    public async Task SaleReturn_RestrictsQuantityToSoldMinusReturned_RefundsAtOriginalPrice()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });
        var compRes = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 5m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 12.50m }
            }
        });
        var sale = compRes.Data!;
        var saleItemId = sale.Items[0].Id;

        // Return 3 units
        var retDto = new CreateSaleReturnDto
        {
            SaleId = sale.Id,
            RefundMethod = PaymentMethod.Cash,
            Reason = "Too many purchased",
            Items = new List<CreateSaleReturnItemDto>
            {
                new() { SaleItemId = saleItemId, ProductId = _trackProd.Id, Quantity = 3m, ReturnToStock = true }
            }
        };

        var ret1 = await _returnService.CreateSaleReturnAsync(retDto);
        ret1.Success.Should().BeTrue();
        ret1.Data!.TotalAmount.Should().Be(7.50m);

        // Attempt returning 3 more (only 2 left returnable) -> must fail
        var ret2 = await _returnService.CreateSaleReturnAsync(retDto);
        ret2.Success.Should().BeFalse();
        ret2.ErrorMessage.Should().Contain("Maximum returnable quantity is 2");
    }

    [Fact]
    public async Task SaleReturn_WithoutReceipt_RespectsSettingAndUsesCurrentPrice()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });

        var returnDto = new CreateSaleReturnDto
        {
            SaleId = null, // No receipt
            RefundMethod = PaymentMethod.Cash,
            Reason = "Lost receipt",
            Items = new List<CreateSaleReturnItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 1m, ReturnToStock = true }
            }
        };

        // 1. Setting AllowReturnWithoutReceipt = false -> fails
        var setting = await _settingService.GetSettingsAsync();
        setting.AllowReturnWithoutReceipt = false;
        await _settingService.UpdateSettingsAsync(setting);

        var fail = await _returnService.CreateSaleReturnAsync(returnDto);
        fail.Success.Should().BeFalse();
        fail.ErrorMessage.Should().Contain("not allowed in store settings");

        // 2. Setting AllowReturnWithoutReceipt = true -> succeeds using Product.SalePrice
        setting.AllowReturnWithoutReceipt = true;
        await _settingService.UpdateSettingsAsync(setting);

        var ok = await _returnService.CreateSaleReturnAsync(returnDto);
        ok.Success.Should().BeTrue();
        ok.Data!.TotalAmount.Should().Be(_trackProd.SalePrice + (_trackProd.SalePrice * (_trackProd.TaxPercent / 100m)));
    }

    [Fact]
    public async Task Shift_OnlyOneOpenAtATime_AndExpectedCashFormula()
    {
        // 1. Open shift 1
        var shift1 = await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });
        shift1.Success.Should().BeTrue();
        int shiftId = shift1.Data!.Id;

        // 2. Attempt to open second shift while shift 1 is open -> fails
        var shift2 = await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 50m });
        shift2.Success.Should().BeFalse();
        shift2.ErrorMessage.Should().Contain("is currently open");

        // 3. Make cash sale of $25
        await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 10m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 25m }
            }
        });

        // 4. Add CashIn $50 (e.g. change money) and CashOut $15 (expenses)
        await _shiftService.AddCashMovementAsync(new CashMovementDto { ShiftId = shiftId, Type = CashMovementType.CashIn, Amount = 50m, Reason = "Float" });
        await _shiftService.AddCashMovementAsync(new CashMovementDto { ShiftId = shiftId, Type = CashMovementType.CashOut, Amount = 15m, Reason = "Tea" });

        // Expected Cash = 100 (open) + 25 (sale) + 50 (in) - 15 (out) = 160
        var summary = await _shiftService.GetShiftSummaryAsync(shiftId);
        summary.Data!.ExpectedCash.Should().Be(160m);

        // 5. Close shift with counted cash = 165 -> difference = +5
        var closeRes = await _shiftService.CloseShiftAsync(new CloseShiftDto { ShiftId = shiftId, CountedCash = 165m });
        closeRes.Success.Should().BeTrue();
        closeRes.Data!.Difference.Should().Be(5.00m);
        closeRes.Data!.Status.Should().Be(ShiftStatus.Closed);

        // 6. Proving closed shift is immutable
        var failCloseAgain = await _shiftService.CloseShiftAsync(new CloseShiftDto { ShiftId = shiftId, CountedCash = 165m });
        failCloseAgain.Success.Should().BeFalse();
        failCloseAgain.ErrorMessage.Should().Contain("already closed and immutable");
    }

    [Fact]
    public async Task CustomerBalanceQuery_CalculatesOpeningPlusDueMinusStoreCredit()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });

        // Customer opening balance = 45
        decimal initialBal = await _customerBalanceQuery.GetCustomerBalanceAsync(_customer.Id);
        initialBal.Should().Be(_customer.OpeningBalance);

        // Complete a credit sale with due amount of $20
        var saleRes = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            CustomerId = _customer.Id,
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 8m, UnitPrice = 2.50m, TaxPercent = 0m } // 20.00
            },
            Payments = new List<SalePaymentDto>() // 0 paid -> 20 due
        });
        saleRes.Success.Should().BeTrue();

        decimal balAfterSale = await _customerBalanceQuery.GetCustomerBalanceAsync(_customer.Id);
        balAfterSale.Should().Be(initialBal + 20m);

        // Post a StoreCredit return of $5
        var retRes = await _returnService.CreateSaleReturnAsync(new CreateSaleReturnDto
        {
            SaleId = saleRes.Data!.Id,
            CustomerId = _customer.Id,
            RefundMethod = PaymentMethod.StoreCredit,
            Reason = "Return against account",
            Items = new List<CreateSaleReturnItemDto>
            {
                new() { SaleItemId = saleRes.Data!.Items[0].Id, ProductId = _trackProd.Id, Quantity = 2m, ReturnToStock = true } // 2 * 2.50 = 5.00
            }
        });
        retRes.Success.Should().BeTrue();

        decimal balAfterReturn = await _customerBalanceQuery.GetCustomerBalanceAsync(_customer.Id);
        balAfterReturn.Should().Be(initialBal + 20m - 5m);
    }

    [Fact]
    public void ProductLookup_ParsesQuickQuantitySyntax()
    {
        var (qty1, code1) = _lookupService.ParseBarcodeQuantity("5*890123456789");
        qty1.Should().Be(5m);
        code1.Should().Be("890123456789");

        var (qty2, code2) = _lookupService.ParseBarcodeQuantity("2.5*BAR-001");
        qty2.Should().Be(2.5m);
        code2.Should().Be("BAR-001");

        var (qty3, code3) = _lookupService.ParseBarcodeQuantity("NORMAL_BARCODE");
        qty3.Should().Be(1m);
        code3.Should().Be("NORMAL_BARCODE");
    }

    [Fact]
    public async Task Numbering_UniquenessUnderParallelCompletion_OnRealSqliteFileInWalMode()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"shoppos_sales_wal_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            using (var ctx = new AppDbContext(options))
            {
                await ctx.Database.MigrateAsync();
                var seeder = new DataSeeder(ctx);
                await seeder.SeedAsync();
            }

            // Run 20 concurrent tasks generating INV numbers
            var numbers = new System.Collections.Concurrent.ConcurrentBag<string>();
            var tasks = Enumerable.Range(1, 20).Select(async _ =>
            {
                using var ctx = new AppDbContext(options);
                var numService = new NumberSequenceService(ctx);
                var num = await numService.GetNextNumberAsync("INV");
                numbers.Add(num);
            });

            await Task.WhenAll(tasks);

            numbers.Should().HaveCount(20);
            numbers.Distinct().Should().HaveCount(20, "each generated invoice number must be strictly unique even with parallel requests");
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { /* ignore lock */ }
            }
        }
    }

    [Fact]
    public async Task Shift_RequireOpenShiftSetting_BlocksSaleIfNoOpenShift()
    {
        var setting = await _settingService.GetSettingsAsync();
        setting.RequireOpenShiftForSales = true;
        await _settingService.UpdateSettingsAsync(setting);

        // No shift is open
        var dto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 1m, UnitPrice = 2.50m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 2.75m }
            }
        };

        var failResult = await _saleService.CompleteSaleAsync(dto);
        failResult.Success.Should().BeFalse();
        failResult.ErrorMessage.Should().Contain("open shift is required");

        // Allow without shift
        setting.RequireOpenShiftForSales = false;
        await _settingService.UpdateSettingsAsync(setting);

        var okResult = await _saleService.CompleteSaleAsync(dto);
        okResult.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteSale_ParallelSalesForLastUnit_ExactlyOneSucceeds()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"shoppos_lastunit_wal_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            int productId;
            using (var ctx = new AppDbContext(options))
            {
                await ctx.Database.MigrateAsync();
                var seeder = new DataSeeder(ctx);
                await seeder.SeedAsync();

                var cat = await ctx.Categories.FirstAsync();
                var u = await ctx.Units.FirstAsync();

                var p = new Product
                {
                    Name = "Last Unit Item",
                    Sku = "LAST-01",
                    Barcode = "999000111",
                    CategoryId = cat.Id,
                    UnitId = u.Id,
                    SalePrice = 10m,
                    CostPrice = 5m,
                    TrackStock = true,
                    IsActive = true
                };
                ctx.Products.Add(p);
                await ctx.SaveChangesAsync();
                productId = p.Id;

                // Exactly 1 unit stock
                ctx.StockMovements.Add(new StockMovement
                {
                    ProductId = productId,
                    MovementType = StockMovementType.Opening,
                    Quantity = 1m,
                    UnitCost = 5m,
                    MovementDate = DateTime.Now
                });

                // Open shift
                var shift = new Shift
                {
                    ShiftNumber = "SHF-000001",
                    OpenedAt = DateTime.Now,
                    OpeningCash = 100m,
                    Status = ShiftStatus.Open
                };
                ctx.Shifts.Add(shift);
                await ctx.SaveChangesAsync();
            }

            var results = new System.Collections.Concurrent.ConcurrentBag<bool>();
            var tasks = Enumerable.Range(1, 2).Select(async _ =>
            {
                using var ctx = new AppDbContext(options);
                var runner = new TransactionRunner(ctx);
                var sRepo = new SaleRepository(ctx);
                var pRepo = new ProductRepository(ctx);
                var cRepo = new CustomerRepository(ctx);
                var mRepo = new Repository<StockMovement>(ctx);
                var slQuery = new StockLedgerQuery(ctx);
                var cbQuery = new CustomerBalanceQuery(ctx);
                var setRepo = new ShopSettingRepository(ctx);
                var setService = new ShopSettingService(setRepo);
                var shRepo = new ShiftRepository(ctx);
                var seqService = new NumberSequenceService(ctx);
                var calc = new SaleCalculator();

                var service = new SaleService(
                    sRepo, pRepo, cRepo, mRepo, slQuery, cbQuery,
                    setService, shRepo, seqService, runner,
                    new Mock<IAuditLogService>().Object, new CurrentUserProvider(),
                    calc, new SaveSaleValidator(), new CompleteSaleValidator());

                var res = await service.CompleteSaleAsync(new CompleteSaleDto
                {
                    Items = new List<SaleLineItemDto>
                    {
                        new() { ProductId = productId, Quantity = 1m, UnitPrice = 10m }
                    },
                    Payments = new List<SalePaymentDto>
                    {
                        new() { Method = PaymentMethod.Cash, Amount = 10m }
                    }
                });

                results.Add(res.Success);
            });

            await Task.WhenAll(tasks);

            results.Count(r => r).Should().Be(1, "exactly one concurrent sale should claim the final unit");
            results.Count(r => !r).Should().Be(1, "the other concurrent sale must fail due to insufficient stock");

            // Verify final ledger stock is exactly 0
            using (var verifyCtx = new AppDbContext(options))
            {
                var slQuery = new StockLedgerQuery(verifyCtx);
                var stock = await slQuery.GetCurrentStockAsync(productId);
                stock.Should().Be(0m);
            }
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task CancelSale_BlockedIfHasReturns_OrClosedShift()
    {
        var shiftRes = await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });
        int shiftId = shiftRes.Data!.Id;

        // Complete sale
        var compRes = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 3m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 7.50m }
            }
        });
        var saleId = compRes.Data!.Id;

        // Post return of 1 item
        await _returnService.CreateSaleReturnAsync(new CreateSaleReturnDto
        {
            SaleId = saleId,
            RefundMethod = PaymentMethod.Cash,
            Reason = "Return 1",
            Items = new List<CreateSaleReturnItemDto>
            {
                new() { SaleItemId = compRes.Data!.Items[0].Id, ProductId = _trackProd.Id, Quantity = 1m, ReturnToStock = true }
            }
        });

        // 1. Attempt cancel -> must fail because returns exist
        var cancelWithReturn = await _saleService.CancelSaleAsync(saleId, "Void sale");
        cancelWithReturn.Success.Should().BeFalse();
        cancelWithReturn.ErrorMessage.Should().Contain("has posted returns");

        // Now test closed shift rule with another sale
        var comp2 = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 1m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 2.50m }
            }
        });
        var sale2Id = comp2.Data!.Id;

        // Close shift
        await _shiftService.CloseShiftAsync(new CloseShiftDto { ShiftId = shiftId, CountedCash = 200m });

        // 2. Attempt cancel -> must fail because shift is closed
        var cancelClosed = await _saleService.CancelSaleAsync(sale2Id, "Void after close");
        cancelClosed.Success.Should().BeFalse();
        cancelClosed.ErrorMessage.Should().Contain("shift");
        cancelClosed.ErrorMessage.Should().Contain("closed");
    }

    [Fact]
    public async Task SaleReturn_Cancel_CreatesReversingMovements_BlockedIfStockWouldGoNegative()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });

        // Sell 5 units (stock goes from 20 to 15)
        var comp = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 5m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 12.50m }
            }
        });
        var sale = comp.Data!;

        // Return 2 units with restock (stock goes from 15 to 17)
        var ret = await _returnService.CreateSaleReturnAsync(new CreateSaleReturnDto
        {
            SaleId = sale.Id,
            RefundMethod = PaymentMethod.Cash,
            Reason = "Return",
            Items = new List<CreateSaleReturnItemDto>
            {
                new() { SaleItemId = sale.Items[0].Id, ProductId = _trackProd.Id, Quantity = 2m, ReturnToStock = true }
            }
        });
        var returnId = ret.Data!.Id;
        (await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id)).Should().Be(17m);

        // Sell 16 units (stock goes from 17 to 1)
        await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 16m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 40m }
            }
        });
        (await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id)).Should().Be(1m);

        // Cancelling return requires reversing restock of 2 units (would make stock 1 - 2 = -1)
        // Setting: AllowNegativeStock = false -> must fail
        var setting = await _settingService.GetSettingsAsync();
        setting.AllowNegativeStock = false;
        await _settingService.UpdateSettingsAsync(setting);

        var failCancel = await _returnService.CancelSaleReturnAsync(returnId, "Cancel return");
        failCancel.Success.Should().BeFalse();
        failCancel.ErrorMessage.Should().Contain("would make stock negative");

        // Allow negative stock -> succeeds
        setting.AllowNegativeStock = true;
        await _settingService.UpdateSettingsAsync(setting);

        var okCancel = await _returnService.CancelSaleReturnAsync(returnId, "Cancel return");
        okCancel.Success.Should().BeTrue();
        (await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id)).Should().Be(-1m);
    }

    [Fact]
    public async Task SaleReturn_RespectsWindowDays()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });

        var comp = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 2m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 5m }
            }
        });
        var sale = comp.Data!;

        // Manually adjust sale date to 15 days ago
        var dbSale = await _context.Sales.FindAsync(sale.Id);
        dbSale!.SaleDate = DateTime.Now.AddDays(-15);
        await _context.SaveChangesAsync();

        // Configure window days = 7
        var setting = await _settingService.GetSettingsAsync();
        setting.SaleReturnWindowDays = 7;
        await _settingService.UpdateSettingsAsync(setting);

        var ret = await _returnService.CreateSaleReturnAsync(new CreateSaleReturnDto
        {
            SaleId = sale.Id,
            RefundMethod = PaymentMethod.Cash,
            Reason = "Too late",
            Items = new List<CreateSaleReturnItemDto>
            {
                new() { SaleItemId = sale.Items[0].Id, ProductId = _trackProd.Id, Quantity = 1m }
            }
        });

        ret.Success.Should().BeFalse();
        ret.ErrorMessage.Should().Contain("allowable return window of 7 days");
    }

    [Fact]
    public async Task SaleReturn_RestockVsDamaged_RestockCreatesMovements_DamagedCreatesNone()
    {
        await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 100m });
        decimal initialStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);

        var comp = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 4m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 10m }
            }
        });
        var sale = comp.Data!;
        // Stock reduced by 4
        (await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id)).Should().Be(initialStock - 4m);

        // Return 1 as restock and 1 as damaged (ReturnToStock = false)
        var ret = await _returnService.CreateSaleReturnAsync(new CreateSaleReturnDto
        {
            SaleId = sale.Id,
            RefundMethod = PaymentMethod.Cash,
            Reason = "1 good, 1 damaged",
            Items = new List<CreateSaleReturnItemDto>
            {
                new() { SaleItemId = sale.Items[0].Id, ProductId = _trackProd.Id, Quantity = 1m, ReturnToStock = true },
                new() { SaleItemId = sale.Items[0].Id, ProductId = _trackProd.Id, Quantity = 1m, ReturnToStock = false }
            }
        });

        ret.Success.Should().BeTrue();

        // Stock should only increase by 1 (the restocked unit, not the damaged one)
        decimal finalStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);
        finalStock.Should().Be(initialStock - 4m + 1m);
    }

    [Fact]
    public async Task ConsistencyTest_SequenceOfSalesReturnsCancels_StockAndBalancesMatch()
    {
        // Initial state:
        // TrackProd stock = 20. Customer opening balance = 45.
        decimal initialStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);
        decimal initialCustBal = await _customerBalanceQuery.GetCustomerBalanceAsync(_customer.Id);

        // 1. Open shift with $200
        var shiftRes = await _shiftService.OpenShiftAsync(new OpenShiftDto { OpeningCash = 200m });
        int shiftId = shiftRes.Data!.Id;

        // 2. Sale 1: 5 units cash @ 2.50 = $12.50
        var s1 = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 5m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 12.50m }
            }
        });
        s1.Success.Should().BeTrue();

        // 3. Sale 2: 3 units credit to customer @ 2.50 = $7.50 (0 paid -> 7.50 due)
        var s2 = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            CustomerId = _customer.Id,
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 3m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>()
        });
        s2.Success.Should().BeTrue();

        // 4. Sale 3: 2 units cash @ 2.50 = $5.00
        var s3 = await _saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = _trackProd.Id, Quantity = 2m, UnitPrice = 2.50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 5.00m }
            }
        });
        s3.Success.Should().BeTrue();

        // 5. Return 1 unit from Sale 1 cash @ 2.50 = $2.50 (restock)
        var r1 = await _returnService.CreateSaleReturnAsync(new CreateSaleReturnDto
        {
            SaleId = s1.Data!.Id,
            RefundMethod = PaymentMethod.Cash,
            Reason = "Return 1",
            Items = new List<CreateSaleReturnItemDto>
            {
                new() { SaleItemId = s1.Data!.Items[0].Id, ProductId = _trackProd.Id, Quantity = 1m, ReturnToStock = true }
            }
        });
        r1.Success.Should().BeTrue();

        // 6. Return 1 unit from Sale 2 StoreCredit @ 2.50 = $2.50 (restock)
        var r2 = await _returnService.CreateSaleReturnAsync(new CreateSaleReturnDto
        {
            SaleId = s2.Data!.Id,
            CustomerId = _customer.Id,
            RefundMethod = PaymentMethod.StoreCredit,
            Reason = "Store credit return",
            Items = new List<CreateSaleReturnItemDto>
            {
                new() { SaleItemId = s2.Data!.Items[0].Id, ProductId = _trackProd.Id, Quantity = 1m, ReturnToStock = true }
            }
        });
        r2.Success.Should().BeTrue();

        // 7. Cancel Sale 3 (reverses 2 units)
        var c3 = await _saleService.CancelSaleAsync(s3.Data!.Id, "Voided Sale 3");
        c3.Success.Should().BeTrue();

        // 8. Shift Cash Movements: CashIn $50, CashOut $20
        await _shiftService.AddCashMovementAsync(new CashMovementDto { ShiftId = shiftId, Type = CashMovementType.CashIn, Amount = 50m, Reason = "Float" });
        await _shiftService.AddCashMovementAsync(new CashMovementDto { ShiftId = shiftId, Type = CashMovementType.CashOut, Amount = 20m, Reason = "Supplies" });

        // Assert 1: Stock calculation matches
        // Initial = 20. S1: -5, S2: -3, S3: -2, R1: +1, R2: +1, CancelS3: +2 -> Expected: 20 - 5 - 3 - 2 + 1 + 1 + 2 = 14
        decimal actualStock = await _stockLedgerQuery.GetCurrentStockAsync(_trackProd.Id);
        actualStock.Should().Be(14m);

        // Assert 2: Customer Balance matches
        // Initial = 45. Due from S2: +7.50. StoreCredit R2: -2.50 -> Expected: 45 + 7.50 - 2.50 = 50.00
        decimal actualCustBal = await _customerBalanceQuery.GetCustomerBalanceAsync(_customer.Id);
        actualCustBal.Should().Be(initialCustBal + 7.50m - 2.50m);

        // Assert 3: Shift Expected Cash matches
        // Open: 200 + NetCashSales (S1: 12.50) - CashRefunds (R1: 2.50) + CashIn (50) - CashOut (20) = 240.00
        // (Note: S3 was cancelled, so excluded from completed sales!)
        var shiftSummary = await _shiftService.GetShiftSummaryAsync(shiftId);
        shiftSummary.Data!.ExpectedCash.Should().Be(240.00m);

        // Assert 4: Payments minus change on every completed sale equals PaidAmount
        var allSales = await _context.Sales.Include(s => s.Payments).Where(s => s.Status == SaleStatus.Completed).ToListAsync();
        foreach (var sale in allSales)
        {
            sale.PaidAmount.Should().Be(sale.Payments.Sum(p => p.Amount));
        }
    }
}