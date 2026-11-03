using AwesomeAssertions;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.HeldBills;
using ShopPOS.UI.ViewModels.Sales;
using Xunit;

namespace ShopPOS.Tests;

public class PosViewModelTests
{
    private readonly Mock<ISaleService> _saleService = new();
    private readonly ISaleCalculator _saleCalculator = new SaleCalculator();
    private readonly Mock<IProductLookupService> _productLookupService = new();
    private readonly Mock<IProductService> _productService = new();
    private readonly Mock<IStockLedgerQuery> _stockLedgerQuery = new();
    private readonly Mock<IShopSettingService> _shopSettingService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<INavigationService> _navigationService = new();
    private readonly Mock<IReceiptPrinterService> _receiptPrinterService = new();

    private readonly ShopSetting _defaultSettings;
    private readonly ProductDto _sampleProduct1Dto;
    private readonly ProductDto _sampleProduct2Dto;
    private readonly Product _sampleProduct1Entity;
    private readonly Product _sampleProduct2Entity;

    public PosViewModelTests()
    {
        _defaultSettings = new ShopSetting
        {
            Id = 1,
            ShopName = "Test Mart",
            Address = "123 High Street",
            Phone = "555-0100",
            CurrencySymbol = "Rs",
            PricesIncludeTax = true,
            CashRoundingStep = 0m,
            AllowNegativeStock = true,
            AllowSellingBelowMinPrice = false,
            AutoPrintReceipt = false
        };

        _shopSettingService.Setup(s => s.GetSettingsAsync())
            .ReturnsAsync(_defaultSettings);



        _saleService.Setup(s => s.ListHeldBillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<SaleDetailDto>>.Ok(new List<SaleDetailDto>()));

        _sampleProduct1Entity = new Product
        {
            Id = 10,
            Name = "Sparkling Water 500ml",
            Sku = "WAT-500",
            Barcode = "123456789012",
            SalePrice = 2.50m,
            MinSalePrice = 2.00m,
            CostPrice = 1.20m,
            TaxPercent = 10.0m,
            AllowDecimalQuantity = false,
            TrackStock = true,
            ReorderLevel = 5m
        };

        _sampleProduct2Entity = new Product
        {
            Id = 20,
            Name = "Organic Chocolate Bar",
            Sku = "CHOC-100",
            Barcode = "987654321098",
            SalePrice = 5.00m,
            MinSalePrice = 4.00m,
            CostPrice = 2.50m,
            TaxPercent = 0.0m,
            AllowDecimalQuantity = false,
            TrackStock = true,
            ReorderLevel = 10m
        };

        _sampleProduct1Dto = new ProductDto
        {
            Id = 10,
            Name = "Sparkling Water 500ml",
            Sku = "WAT-500",
            Barcode = "123456789012",
            SalePrice = 2.50m,
            MinSalePrice = 2.00m,
            CostPrice = 1.20m,
            TaxPercent = 10.0m,
            AllowDecimalQuantity = false,
            TrackStock = true,
            ReorderLevel = 5m
        };

        _sampleProduct2Dto = new ProductDto
        {
            Id = 20,
            Name = "Organic Chocolate Bar",
            Sku = "CHOC-100",
            Barcode = "987654321098",
            SalePrice = 5.00m,
            MinSalePrice = 4.00m,
            CostPrice = 2.50m,
            TaxPercent = 0.0m,
            AllowDecimalQuantity = false,
            TrackStock = true,
            ReorderLevel = 10m
        };

        _productService.Setup(p => p.GetProductByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProductDto>.Ok(_sampleProduct1Dto));

        _productService.Setup(p => p.GetProductByIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProductDto>.Ok(_sampleProduct2Dto));

        _stockLedgerQuery.Setup(s => s.GetCurrentStockAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15m);

        _stockLedgerQuery.Setup(s => s.GetCurrentStockAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(8m);

    }

    private PosSaleViewModel CreateViewModel()
    {
        var mockPriceLevelService = new Mock<IPriceLevelService>();
        mockPriceLevelService.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<PriceLevelDto>>.Ok(new List<PriceLevelDto>()));

        return new PosSaleViewModel(
            _saleService.Object,
            _saleCalculator,
            _productLookupService.Object,
            _productService.Object,
            mockPriceLevelService.Object,
            _stockLedgerQuery.Object,
            _shopSettingService.Object,
            _dialogService.Object,
            _navigationService.Object,
            _receiptPrinterService.Object);
    }

    [Fact]
    public async Task PosSaleViewModel_ScanBarcode_ShouldAddOrIncrementLine()
    {
        // Arrange
        _productLookupService.Setup(l => l.LookupProductAsync("123456789012", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductLookupResult
            {
                Found = true,
                ParsedQuantity = 1m,
                Product = _sampleProduct1Entity
            });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act 1: First scan
        vm.ScanInput = "123456789012";
        await vm.ProcessScanInputAsync();

        // Assert 1: Line item added
        vm.CartItems.Should().HaveCount(1);
        vm.CartItems[0].ProductId.Should().Be(10);
        vm.CartItems[0].Quantity.Should().Be(1m);
        vm.CartItems[0].UnitPrice.Should().Be(2.50m);
        vm.ItemsCount.Should().Be(1);
        vm.TotalUnits.Should().Be(1m);
        vm.ScanInput.Should().BeEmpty();
        vm.ScanErrorMessage.Should().BeNull();

        // Act 2: Second scan with same barcode
        vm.ScanInput = "123456789012";
        await vm.ProcessScanInputAsync();

        // Assert 2: Existing line incremented, not duplicated
        vm.CartItems.Should().HaveCount(1);
        vm.CartItems[0].Quantity.Should().Be(2m);
        vm.ItemsCount.Should().Be(1);
        vm.TotalUnits.Should().Be(2m);
        vm.GrandTotal.Should().Be(5.00m);
    }

    [Fact]
    public async Task PosSaleViewModel_MultiplierBarcode_ShouldSetSpecifiedQuantity()
    {
        // Arrange
        _productLookupService.Setup(l => l.LookupProductAsync("5*123456789012", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductLookupResult
            {
                Found = true,
                ParsedQuantity = 5m,
                Product = _sampleProduct1Entity
            });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act
        vm.ScanInput = "5*123456789012";
        await vm.ProcessScanInputAsync();

        // Assert
        vm.CartItems.Should().HaveCount(1);
        vm.CartItems[0].ProductId.Should().Be(10);
        vm.CartItems[0].Quantity.Should().Be(5m);
        vm.TotalUnits.Should().Be(5m);
        vm.GrandTotal.Should().Be(12.50m);
    }

    [Fact]
    public async Task PosSaleViewModel_UnknownBarcode_ShouldSetErrorMessage()
    {
        // Arrange
        _productLookupService.Setup(l => l.LookupProductAsync("UNKNOWN999", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductLookupResult
            {
                Found = false,
                ErrorMessage = "No product found matching code 'UNKNOWN999'."
            });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act
        vm.ScanInput = "UNKNOWN999";
        await vm.ProcessScanInputAsync();

        // Assert
        vm.CartItems.Should().BeEmpty();
        vm.ScanErrorMessage.Should().Contain("UNKNOWN999");
    }

    [Fact]
    public async Task PosSaleViewModel_CartTotals_ShouldCalculateFromSaleCalculator()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Add 2 of product 1 ($2.50 each with 10% tax incl) and 1 of product 2 ($5.00 each with 0% tax)
        await vm.AddProductToCartAsync(10, 2m);
        await vm.AddProductToCartAsync(20, 1m);

        // Pre-discount total = 5.00 + 5.00 = 10.00
        vm.SubTotal.Should().Be(10.00m);

        // Apply bill discount of $1.00
        vm.BillDiscountAmount = 1.00m;
        vm.RecalculateTotals();

        // Assert totals computed via ISaleCalculator
        vm.GrandTotal.Should().Be(9.00m);
        vm.BillDiscountAmount.Should().Be(1.00m);
        vm.CartItems.Should().HaveCount(2);
        vm.TotalUnits.Should().Be(3m);
    }

    [Fact]
    public async Task PosSaleViewModel_QuantityEditsAndLineRemoval_ShouldUpdateTotals()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        await vm.AddProductToCartAsync(10, 1m);

        var item = vm.CartItems[0];
        vm.GrandTotal.Should().Be(2.50m);

        // Act 1: Increment quantity
        item.IncrementQuantityCommand.Execute(null);
        item.Quantity.Should().Be(2m);
        vm.GrandTotal.Should().Be(5.00m);

        // Act 2: Decrement quantity
        item.DecrementQuantityCommand.Execute(null);
        item.Quantity.Should().Be(1m);
        vm.GrandTotal.Should().Be(2.50m);

        // Act 3: Remove line item
        vm.RemoveCartLine(item);
        vm.CartItems.Should().BeEmpty();
        vm.GrandTotal.Should().Be(0m);
        vm.ItemsCount.Should().Be(0);
        vm.HasCartItems.Should().BeFalse();
    }

    [Fact]
    public async Task PosSaleViewModel_HoldAndResumeBill_ShouldPreserveItemsAndCustomer()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        await vm.AddProductToCartAsync(10, 3m);
        await vm.AddProductToCartAsync(20, 2m);

        vm.Notes = "Hold for customer pickup";
        vm.BillDiscountAmount = 2.00m;

        _saleService.Setup(s => s.SaveHeldBillAsync(It.IsAny<SaveSaleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SaleDetailDto>.Ok(new SaleDetailDto
            {
                Id = 101,
                HoldNumber = "HOLD-0001",
                GrandTotal = vm.GrandTotal,
                SaleDate = DateTime.UtcNow
            }));

        // Act 1: Hold bill
        await vm.HoldBillAsync();

        // Assert 1: Cart is cleared and SaveHeldBillAsync was called with exact items
        _saleService.Verify(s => s.SaveHeldBillAsync(It.Is<SaveSaleDto>(dto =>
            dto.Notes == "Hold for customer pickup" &&
            dto.BillDiscountAmount == 2.00m &&
            dto.Items.Count == 2), It.IsAny<CancellationToken>()), Times.Once);

        vm.CartItems.Should().BeEmpty();
        vm.HasCartItems.Should().BeFalse();

        // Act 2: Resume held bill
        var heldSale = new SaleDetailDto
        {
            Id = 101,
            SaleNumber = "HOLD-0001",
            Notes = "Hold for customer pickup",
            BillDiscountAmount = 2.00m,
            GrandTotal = 15.50m,
            Items = new List<SaleItemDetailDto>
            {
                new() { ProductId = 10, Quantity = 3m, UnitPrice = 2.50m },
                new() { ProductId = 20, Quantity = 2m, UnitPrice = 5.00m }
            }
        };

        await vm.ResumeHeldBillAsync(heldSale);

        // Assert 2: Cart state and customer restored
        vm.OriginatingHeldBillId.Should().Be(101);
        vm.Notes.Should().Be("Hold for customer pickup");
        vm.CartItems.Should().HaveCount(2);
        vm.CartItems.First(i => i.ProductId == 10).Quantity.Should().Be(3m);
        vm.CartItems.First(i => i.ProductId == 20).Quantity.Should().Be(2m);
    }

    [Fact]
    public async Task PosSaleViewModel_ClearCart_PromptHandling()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        await vm.AddProductToCartAsync(10, 1m);

        // Case 1: User chooses Keep -> Cart is NOT cleared
        _dialogService.Setup(d => d.ShowCartExitPrompt()).Returns(CartExitAction.Keep);
        vm.ClearCart();
        vm.CartItems.Should().HaveCount(1);

        // Case 2: User chooses Discard -> Cart IS cleared
        _dialogService.Setup(d => d.ShowCartExitPrompt()).Returns(CartExitAction.Discard);
        vm.ClearCart();
        vm.CartItems.Should().BeEmpty();

        // Case 3: User chooses Hold -> SaveHeldBillAsync is called
        await vm.AddProductToCartAsync(10, 1m);
        _dialogService.Setup(d => d.ShowCartExitPrompt()).Returns(CartExitAction.Hold);
        _saleService.Setup(s => s.SaveHeldBillAsync(It.IsAny<SaveSaleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SaleDetailDto>.Ok(new SaleDetailDto { Id = 102, HoldNumber = "HOLD-0002" }));

        vm.ClearCart();
        _saleService.Verify(s => s.SaveHeldBillAsync(It.IsAny<SaveSaleDto>(), It.IsAny<CancellationToken>()), Times.Once);
        vm.CartItems.Should().BeEmpty();
    }

    [Fact]
    public void SalePaymentDialogViewModel_CashAndChangeCalculations()
    {
        // Scenario 1: Exact cash payment
        var vm = new SalePaymentDialogViewModel(
            grandTotal: 80.00m,
            customerId: null,
            customerName: "Walk-in Customer",
            customerCurrentBalance: 0m,
            customerCreditLimit: 0m,
            allowCreditSales: true,
            currencySymbol: "Rs",
            saleCalculator: _saleCalculator);

        vm.ChangeAmount.Should().Be(0m);
        vm.DueAmount.Should().Be(0m);
        vm.IsValid.Should().BeTrue();

        // Scenario 2: Overpaying cash ($100 tendered for $80 total)
        vm.SetCashAmount(100.00m);
        vm.ChangeAmount.Should().Be(20.00m);
        vm.DueAmount.Should().Be(0m);
        vm.IsValid.Should().BeTrue();

        // Scenario 3: Non-cash over-tender ($100 Card for $80 total)
        vm.Payments.Clear();
        var cardLine = new PaymentLineViewModel { Method = PaymentMethod.Card, Amount = 100.00m };
        vm.Payments.Add(cardLine);
        vm.Recalculate();

        vm.IsValid.Should().BeFalse();
        vm.ErrorMessage.Should().Contain("Non-cash payments");

        // Scenario 4: Partial payment (underpayment) rejected by full payment rule
        vm.Payments.Clear();
        vm.Payments.Add(new PaymentLineViewModel { Method = PaymentMethod.Cash, Amount = 50.00m });
        vm.Recalculate();

        vm.IsValid.Should().BeFalse();
        vm.ErrorMessage.Should().Contain("Full payment required");

        // Scenario 5: Split payment (Card $50 + Cash $30 = $80) is valid
        vm.Payments.Clear();
        vm.Payments.Add(new PaymentLineViewModel { Method = PaymentMethod.Card, Amount = 50.00m });
        vm.Payments.Add(new PaymentLineViewModel { Method = PaymentMethod.Cash, Amount = 30.00m });
        vm.Recalculate();

        vm.IsValid.Should().BeTrue();
        vm.ChangeAmount.Should().Be(0m);
        vm.DueAmount.Should().Be(0m);

        // Scenario 6: Split payment with cash over-tender (Card $50 + Cash $40 = $90 for $80 total)
        vm.Payments.Clear();
        vm.Payments.Add(new PaymentLineViewModel { Method = PaymentMethod.Card, Amount = 50.00m });
        vm.Payments.Add(new PaymentLineViewModel { Method = PaymentMethod.Cash, Amount = 40.00m });
        vm.Recalculate();

        vm.IsValid.Should().BeTrue();
        vm.ChangeAmount.Should().Be(10.00m);
        vm.DueAmount.Should().Be(0m);
    }

    [Fact]
    public async Task PosSaleViewModel_CompleteSale_SuccessFlow()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        await vm.AddProductToCartAsync(10, 2m);

        var completedSaleDto = new SaleDetailDto
        {
            Id = 55,
            SaleNumber = "INV-2026-0001",
            SaleDate = DateTime.UtcNow,
            GrandTotal = 5.00m,
            PaidAmount = 5.00m,
            Status = SaleStatus.Completed
        };

        _dialogService.Setup(d => d.ShowSalePaymentDialog(It.IsAny<SalePaymentDialogViewModel>()))
            .Callback<SalePaymentDialogViewModel>(pvm =>
            {
                pvm.Confirmed = true;
                pvm.ResultPayments.Add(new SalePaymentDto { Method = PaymentMethod.Cash, Amount = 5.00m, TenderedAmount = 5.00m });
            })
            .Returns(true);

        _saleService.Setup(s => s.CompleteSaleAsync(It.IsAny<CompleteSaleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SaleDetailDto>.Ok(completedSaleDto));

        // Act
        await vm.OpenPaymentAsync();

        // Assert
        _saleService.Verify(s => s.CompleteSaleAsync(It.IsAny<CompleteSaleDto>(), It.IsAny<CancellationToken>()), Times.Once);
        vm.IsSaleSuccess.Should().BeTrue();
        vm.LastCompletedSale.Should().NotBeNull();
        vm.LastCompletedSale!.SaleNumber.Should().Be("INV-2026-0001");

        // Act 2: Start new sale after success
        vm.StartNewSaleAfterSuccess();
        vm.IsSaleSuccess.Should().BeFalse();
        vm.CartItems.Should().BeEmpty();
    }

    [Fact]
    public async Task PosSaleViewModel_CompleteSale_BackendFailureLeavesCartIntact()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        await vm.AddProductToCartAsync(10, 2m);

        _dialogService.Setup(d => d.ShowSalePaymentDialog(It.IsAny<SalePaymentDialogViewModel>()))
            .Callback<SalePaymentDialogViewModel>(pvm =>
            {
                pvm.Confirmed = true;
                pvm.ResultPayments.Add(new SalePaymentDto { Method = PaymentMethod.Cash, Amount = 5.00m });
            })
            .Returns(true);

        _saleService.Setup(s => s.CompleteSaleAsync(It.IsAny<CompleteSaleDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SaleDetailDto>.Failure("Database lock timeout while posting transaction."));

        // Act
        await vm.OpenPaymentAsync();

        // Assert: Cart items must NOT be lost on failure
        _dialogService.Verify(d => d.ShowMessage(It.Is<string>(msg => msg.Contains("Database lock")), "Sale Failed"), Times.Once);
        vm.IsSaleSuccess.Should().BeFalse();
        vm.CartItems.Should().HaveCount(1);
        vm.CartItems[0].Quantity.Should().Be(2m);
    }

    
    [Fact]
    public void SaleReturnDialogViewModel_QuantityEnforcement()
    {
        // Arrange
        var returnableLine = new ReturnableLineDto
        {
            SaleItemId = 1,
            ProductId = 10,
            ProductName = "Sparkling Water 500ml",
            Sku = "WAT-500",
            SoldQuantity = 5m,
            AlreadyReturnedQuantity = 2m,
            RemainingQuantity = 3m,
            OriginalUnitPrice = 2.50m,
            OriginalTaxPercent = 10m,
            LineTotal = 12.50m
        };

        var lineVm = new ReturnLineItemViewModel(returnableLine);

        // Assert initial clamped quantity
        lineVm.AvailableReturnQuantity.Should().Be(3m);
        lineVm.ReturnQuantity.Should().Be(1m);

        // Act 1: User tries to return 5 items (only 3 available)
        lineVm.ReturnQuantity = 5m;

        // Assert 1: Clamped to AvailableReturnQuantity
        lineVm.ReturnQuantity.Should().Be(3m);
        lineVm.EstimatedLineRefund.Should().Be(7.50m); // 3 * 2.50
    }

    [Fact]
    public async Task ScreenOperations_WithoutChanges_MustNeverCreateStockMovementsOrPayments()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Cancel out of payment dialog without confirming
        _dialogService.Setup(d => d.ShowSalePaymentDialog(It.IsAny<SalePaymentDialogViewModel>()))
            .Returns(false);

        await vm.AddProductToCartAsync(10, 1m);
        await vm.OpenPaymentAsync();

        // Assert: 0 calls to CompleteSaleAsync
        _saleService.Verify(s => s.CompleteSaleAsync(It.IsAny<CompleteSaleDto>(), It.IsAny<CancellationToken>()), Times.Never);

        // Resuming a held bill without completing it creates 0 sales or payments
        var heldSale = new SaleDetailDto
        {
            Id = 102,
            SaleNumber = "HOLD-0002",
            GrandTotal = 2.50m,
            Items = new List<SaleItemDetailDto> { new() { ProductId = 10, Quantity = 1m, UnitPrice = 2.50m } }
        };
        await vm.ResumeHeldBillAsync(heldSale);

        _saleService.Verify(s => s.CompleteSaleAsync(It.IsAny<CompleteSaleDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    }
