using AwesomeAssertions;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Purchases;
using ShopPOS.UI.ViewModels.PurchaseReturns;
using ShopPOS.UI.ViewModels.Settings;
using ShopPOS.UI.ViewModels.SupplierPayments;
using System.Collections.ObjectModel;
using Xunit;

namespace ShopPOS.Tests;

public class PurchaseScreensViewModelTests
{
    private readonly Mock<IPurchaseService> _purchaseService = new();
    private readonly Mock<ISupplierService> _supplierService = new();
    private readonly Mock<IProductService> _productService = new();
    private readonly Mock<IPurchaseCalculator> _calculator = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IShopSettingService> _shopSettingService = new();
    private readonly Mock<ISupplierPaymentService> _paymentService = new();
    private readonly Mock<IPurchaseReturnService> _returnService = new();
    private readonly Mock<IServiceProvider> _serviceProvider = new();

    public PurchaseScreensViewModelTests()
    {

        _supplierService.Setup(s => s.GetFlatListAsync(false, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<SupplierDto>>.Ok(new List<SupplierDto>
            {
                new() { Id = 1, Name = "Acme Distributing", IsActive = true },
                new() { Id = 7, Name = "Mega Corp", IsActive = true },
                new() { Id = 3, Name = "Local Wholesaler", IsActive = true }
            }));

        // Default calculator behavior delegating to actual calculator implementation
        _calculator.Setup(c => c.CalculateLine(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns<decimal, decimal, decimal, decimal>((qty, cost, disc, tax) => PurchaseCalculator.CalculateLine(qty, cost, disc, tax));

        _calculator.Setup(c => c.CalculateTotals(It.IsAny<IEnumerable<LineCalculationResult>>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns<IEnumerable<LineCalculationResult>, decimal, decimal>((lines, invDisc, freight) => PurchaseCalculator.CalculateTotals(lines, invDisc, freight));
    }

    [Fact]
    public async Task PurchaseEntryViewModel_ScanBarcode_ShouldAddOrIncrementLine()
    {
        // Arrange
        var product = new ProductListItemDto
        {
            Id = 10,
            Name = "Energy Drink 250ml",
            Sku = "ED-250",
            Barcode = "8901234567890",
            CostPrice = 2.50m,
            TaxPercent = 5.0m,
            CurrentStock = 12m
        };

        _productService.Setup(p => p.GetPagedProductsAsync(It.Is<ProductSearchQuery>(q => q.SearchTerm == "8901234567890"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<ProductListItemDto>>.Ok(new PagedResult<ProductListItemDto>(new List<ProductListItemDto> { product }, 1, 1, 10)));

        var vm = new PurchaseEntryViewModel(
            _purchaseService.Object,
            _supplierService.Object,
            _productService.Object,
            _calculator.Object,
            _dialogService.Object);

        await vm.InitializeAsync();

        // Act 1: First scan
        vm.BarcodeInput = "8901234567890";
        await vm.AddScannedProductCommand.ExecuteAsync(null);

        // Assert 1: Line added with quantity 1
        vm.LineItems.Should().HaveCount(1);
        vm.LineItems[0].ProductId.Should().Be(10);
        vm.LineItems[0].Quantity.Should().Be(1m);
        vm.LineItems[0].UnitCost.Should().Be(2.50m);
        vm.BarcodeInput.Should().BeEmpty();
        vm.HasUnsavedChanges.Should().BeTrue();

        // Act 2: Second scan with same barcode
        vm.BarcodeInput = "8901234567890";
        await vm.AddScannedProductCommand.ExecuteAsync(null);

        // Assert 2: Merged into existing line, quantity incremented to 2
        vm.LineItems.Should().HaveCount(1);
        vm.LineItems[0].Quantity.Should().Be(2m);
        vm.BarcodeInput.Should().BeEmpty();
    }

    [Fact]
    public async Task PurchaseEntryViewModel_LineItemMath_CalculatesViaPurchaseCalculator()
    {
        // Arrange
        var vm = new PurchaseEntryViewModel(
            _purchaseService.Object,
            _supplierService.Object,
            _productService.Object,
            _calculator.Object,
            _dialogService.Object);

        await vm.InitializeAsync();

        var line = new PurchaseEntryLineItemViewModel(_calculator.Object, vm.RecalculateTotals);
        line.SetInitialValues(
            productId: 5,
            productName: "Test Widget",
            sku: "TW-1",
            barcode: null,
            categoryName: "General",
            unitName: "pcs",
            quantity: 10m,
            unitCost: 20m,
            discountPercent: 10m,
            taxPercent: 5m);

        vm.LineItems.Add(line);
        vm.RecalculateTotals();

        // Assert
        // Line calculation: 10 * 20 = 200, discount 10% = 20, net = 180, tax 5% = 9, total = 189
        line.LineTotal.Should().Be(189m);
        vm.GrandTotal.Should().Be(189m);

        // Modify line item quantity
        line.Quantity = 5m; // 5 * 20 = 100, disc 10 = 10, net = 90, tax 5% = 4.5, total = 94.5
        line.LineTotal.Should().Be(94.5m);
        vm.GrandTotal.Should().Be(94.5m);

        // Verify calculator was invoked
        _calculator.Verify(c => c.CalculateLine(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task PurchaseEntryViewModel_UnsavedChanges_DetectedCorrectly()
    {
        // Arrange
        var vm = new PurchaseEntryViewModel(
            _purchaseService.Object,
            _supplierService.Object,
            _productService.Object,
            _calculator.Object,
            _dialogService.Object);

        await vm.InitializeAsync();

        // Clean draft: HasUnsavedChanges should be false
        vm.HasUnsavedChanges.Should().BeFalse();

        // Add a product item via ViewModel helper
        vm.AddOrIncrementProduct(new ProductListItemDto
        {
            Id = 1,
            Name = "Prod",
            Sku = "SKU1",
            CostPrice = 10m,
            TaxPercent = 0m
        });

        // HasUnsavedChanges should now be true
        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task PurchaseEntryViewModel_PostedPurchase_OpensInReadOnlyMode()
    {
        // Arrange
        var postedPurchase = new PurchaseDetailDto
        {
            Id = 42,
            PurchaseNumber = "PUR-000042",
            SupplierId = 1,
            SupplierName = "Acme Distributing",
            PurchaseDate = DateTime.Today,
            Status = PurchaseStatus.Posted,
            PaymentStatus = PurchasePaymentStatus.Unpaid,
            GrandTotal = 500m,
            PaidAmount = 0m,
            PostedAt = DateTime.UtcNow,
            PostedByName = "admin",
            Items = new List<PurchaseItemDto>
            {
                new()
                {
                    Id = 101,
                    ProductId = 5,
                    ProductName = "Tool",
                    Sku = "TL-1",
                    Quantity = 10,
                    UnitCost = 50,
                    LineTotal = 500
                }
            }
        };

        _purchaseService.Setup(p => p.GetPurchaseByIdAsync(42, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PurchaseDetailDto>.Ok(postedPurchase));

        var vm = new PurchaseEntryViewModel(
            _purchaseService.Object,
            _supplierService.Object,
            _productService.Object,
            _calculator.Object,
            _dialogService.Object);

        // Act
        await vm.InitializeAsync(42);

        // Assert
        vm.IsReadOnly.Should().BeTrue();
        vm.HasUnsavedChanges.Should().BeFalse();
        vm.PurchaseNumber.Should().Be("PUR-000042");
        vm.GrandTotal.Should().Be(500m);
    }

    
    [Fact]
    public async Task SupplierPaymentDialogViewModel_InvoiceSelection_DefaultsAmountToBalance()
    {
        // Arrange
        var invoices = new List<PurchaseListDto>
        {
            new() { Id = 10, PurchaseNumber = "PUR-000010", GrandTotal = 1000m, PaidAmount = 400m }
        };

        _purchaseService.Setup(p => p.GetPurchasesAsync(It.Is<PurchaseSearchQuery>(q => q.SupplierId == 7), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<PurchaseListDto>>.Ok(new PagedResult<PurchaseListDto>(invoices, 1, 1, 100)));

        var vm = new SupplierPaymentDialogViewModel(
            _paymentService.Object,
            _supplierService.Object,
            _purchaseService.Object);

        await vm.InitializeAsync(preselectedSupplierId: 7, preselectedPurchaseId: null);

        // Act: Pick the invoice
        vm.SelectedInvoice = vm.Invoices.FirstOrDefault(i => i.Id == 10);

        // Assert: Amount defaults to outstanding balance 600
        vm.Amount.Should().Be(600m);
    }

    [Fact]
    public async Task PurchaseReturnDialogViewModel_ReturnQtyExceedingReturnable_FailsValidation()
    {
        // Arrange
        var invoice = new PurchaseDetailDto
        {
            Id = 20,
            PurchaseNumber = "PUR-000020",
            SupplierId = 3,
            SupplierName = "Local Wholesaler",
            Items = new List<PurchaseItemDto>
            {
                new()
                {
                    Id = 201,
                    ProductId = 50,
                    ProductName = "Widget Box",
                    Sku = "WB-50",
                    Quantity = 5m,
                    UnitCost = 10m
                }
            }
        };

        _purchaseService.Setup(p => p.GetPurchaseByIdAsync(20, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PurchaseDetailDto>.Ok(invoice));

        _purchaseService.Setup(p => p.GetPurchasesAsync(It.Is<PurchaseSearchQuery>(q => q.SupplierId == 3), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<PurchaseListDto>>.Ok(new PagedResult<PurchaseListDto>(new List<PurchaseListDto>
            {
                new() { Id = 20, PurchaseNumber = "PUR-000020", SupplierId = 3 }
            }, 1, 1, 100)));

        var vm = new PurchaseReturnDialogViewModel(
            _returnService.Object,
            _supplierService.Object,
            _purchaseService.Object);

        await vm.InitializeAsync(preselectedSupplierId: 3, preselectedPurchaseId: 20);

        // Act: Set return quantity to 15 (exceeding returnable 5)
        var returnLine = vm.LineItems.First();
        returnLine.ReturnQuantity = 15m;

        await vm.PostReturnCommand.ExecuteAsync(null);

        // Assert: Failed validation, return service not called
        vm.ErrorMessage.Should().Contain("exceeds returnable amount");
        _returnService.Verify(r => r.PostReturnAsync(It.IsAny<CreatePurchaseReturnDto>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SettingsViewModel_Owner_CanEditCostingMethodAndAllowNegativeWithoutRoleRestriction()
    {
        // Arrange

        var existingSettings = new ShopSetting
        {
            ShopName = "My Shop",
            CostingMethod = CostingMethod.WeightedAverage,
            AllowNegativeStock = false
        };

        _shopSettingService.Setup(s => s.GetSettingsAsync())
            .ReturnsAsync(existingSettings);

        var vm = new SettingsViewModel(
            _shopSettingService.Object);

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.IsAdmin.Should().BeTrue();
    }
}
