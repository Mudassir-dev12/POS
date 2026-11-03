using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Inventory;
using ShopPOS.UI.ViewModels.Purchases;
using ShopPOS.UI.ViewModels.Sales;
using Xunit;

namespace ShopPOS.Tests;

public class BarcodeScannerIntegrationTests
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
    private readonly Mock<IReceiptBuilder> _receiptBuilder = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<ICurrentUserProvider> _currentUserProvider = new();
    private readonly Mock<IPriceLevelService> _priceLevelService = new();
    private readonly Mock<IProductSearchService> _productSearchService = new();

    private readonly Product _cokeEntity;
    private readonly ProductDto _cokeDto;

    public BarcodeScannerIntegrationTests()
    {
        _cokeEntity = new Product
        {
            Id = 101,
            Name = "Coca Cola 500ml",
            Sku = "P000001",
            Barcode = "8901030000018",
            SalePrice = 75.00m,
            CostPrice = 55.00m,
            TrackStock = true,
            ReorderLevel = 10,
            IsActive = true
        };

        _cokeDto = new ProductDto
        {
            Id = 101,
            Name = "Coca Cola 500ml",
            Sku = "P000001",
            Barcode = "8901030000018",
            SalePrice = 75.00m,
            CostPrice = 55.00m,
            TrackStock = true,
            ReorderLevel = 10,
            IsActive = true
        };

        _shopSettingService.Setup(s => s.GetSettingsAsync())
            .ReturnsAsync(new ShopSetting
            {
                ShopName = "Test Retail Mart",
                CurrencySymbol = "Rs",
                CurrencyDecimals = 2,
                AllowNegativeStock = true
            });

        _priceLevelService.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<PriceLevelDto>>.Ok(new List<PriceLevelDto>
            {
                new() { Id = 1, Name = "Retail" }
            }));

        _saleService.Setup(s => s.ListHeldBillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<SaleDetailDto>>.Ok(new List<SaleDetailDto>()));

        _stockLedgerQuery.Setup(s => s.GetCurrentStockAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50m);

        _productService.Setup(p => p.GetProductByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProductDto>.Ok(_cokeDto));
    }

    [Fact]
    public async Task PosSaleViewModel_ScanExactBarcode_AddsProductToCartWithUnitQuantity()
    {
        // Arrange
        _productLookupService.Setup(p => p.LookupProductAsync("8901030000018", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductLookupResult
            {
                Found = true,
                Product = _cokeEntity,
                ParsedQuantity = 1m,
                ResolvedUnitPrice = 75.00m
            });

        var vm = new PosSaleViewModel(
            _saleService.Object,
            _saleCalculator,
            _productLookupService.Object,
            _productService.Object,
            _priceLevelService.Object,
            _stockLedgerQuery.Object,
            _shopSettingService.Object,
            _dialogService.Object,
            _navigationService.Object,
            _receiptPrinterService.Object,
            _receiptBuilder.Object,
            _auditLogService.Object,
            _currentUserProvider.Object,
            _productSearchService.Object);

        await vm.LoadSettingsAndPriceLevelsAsync();

        // Act: Simulate barcode scanner wedge input with Enter
        vm.ScanInput = "8901030000018";
        await vm.ProcessScanInputCommand.ExecuteAsync(null);

        // Assert
        vm.CartItems.Should().HaveCount(1);
        var item = vm.CartItems[0];
        item.ProductId.Should().Be(101);
        item.ProductName.Should().Be("Coca Cola 500ml");
        item.Quantity.Should().Be(1m);
        item.UnitPrice.Should().Be(75.00m);
        item.LineTotal.Should().Be(75.00m);
        vm.ScanInput.Should().BeEmpty();
        vm.ScanErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task PosSaleViewModel_RepeatScanSameBarcode_IncrementsQuantityInCart()
    {
        // Arrange
        _productLookupService.Setup(p => p.LookupProductAsync("8901030000018", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductLookupResult
            {
                Found = true,
                Product = _cokeEntity,
                ParsedQuantity = 1m,
                ResolvedUnitPrice = 75.00m
            });

        var vm = new PosSaleViewModel(
            _saleService.Object,
            _saleCalculator,
            _productLookupService.Object,
            _productService.Object,
            _priceLevelService.Object,
            _stockLedgerQuery.Object,
            _shopSettingService.Object,
            _dialogService.Object,
            _navigationService.Object,
            _receiptPrinterService.Object,
            _receiptBuilder.Object,
            _auditLogService.Object,
            _currentUserProvider.Object,
            _productSearchService.Object);

        await vm.LoadSettingsAndPriceLevelsAsync();

        // Act 1: First scan
        vm.ScanInput = "8901030000018";
        await vm.ProcessScanInputCommand.ExecuteAsync(null);

        // Act 2: Second scan of the same barcode
        vm.ScanInput = "8901030000018";
        await vm.ProcessScanInputCommand.ExecuteAsync(null);

        // Assert
        vm.CartItems.Should().HaveCount(1);
        vm.CartItems[0].Quantity.Should().Be(2m);
        vm.CartItems[0].LineTotal.Should().Be(150.00m);
        vm.GrandTotal.Should().Be(150.00m);
    }

    [Fact]
    public async Task PosSaleViewModel_ScanMultiplierPrefix_AddsParsedQuantityToCart()
    {
        // Arrange: E.g. "4*8901030000018"
        _productLookupService.Setup(p => p.LookupProductAsync("4*8901030000018", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductLookupResult
            {
                Found = true,
                Product = _cokeEntity,
                ParsedQuantity = 4m,
                ResolvedUnitPrice = 75.00m
            });

        var vm = new PosSaleViewModel(
            _saleService.Object,
            _saleCalculator,
            _productLookupService.Object,
            _productService.Object,
            _priceLevelService.Object,
            _stockLedgerQuery.Object,
            _shopSettingService.Object,
            _dialogService.Object,
            _navigationService.Object,
            _receiptPrinterService.Object,
            _receiptBuilder.Object,
            _auditLogService.Object,
            _currentUserProvider.Object,
            _productSearchService.Object);

        await vm.LoadSettingsAndPriceLevelsAsync();

        // Act
        vm.ScanInput = "4*8901030000018";
        await vm.ProcessScanInputCommand.ExecuteAsync(null);

        // Assert
        vm.CartItems.Should().HaveCount(1);
        vm.CartItems[0].Quantity.Should().Be(4m);
        vm.CartItems[0].LineTotal.Should().Be(300.00m);
    }

    [Fact]
    public async Task PosSaleViewModel_ScanInvalidBarcode_DisplaysErrorMessageAndDoesNotAddToCart()
    {
        // Arrange
        _productLookupService.Setup(p => p.LookupProductAsync("INVALID_CODE_999", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductLookupResult
            {
                Found = false,
                ErrorMessage = "Unknown barcode or product 'INVALID_CODE_999'."
            });

        var vm = new PosSaleViewModel(
            _saleService.Object,
            _saleCalculator,
            _productLookupService.Object,
            _productService.Object,
            _priceLevelService.Object,
            _stockLedgerQuery.Object,
            _shopSettingService.Object,
            _dialogService.Object,
            _navigationService.Object,
            _receiptPrinterService.Object,
            _receiptBuilder.Object,
            _auditLogService.Object,
            _currentUserProvider.Object,
            _productSearchService.Object);

        await vm.LoadSettingsAndPriceLevelsAsync();

        // Act
        vm.ScanInput = "INVALID_CODE_999";
        await vm.ProcessScanInputCommand.ExecuteAsync(null);

        // Assert
        vm.CartItems.Should().BeEmpty();
        vm.ScanErrorMessage.Should().Contain("INVALID_CODE_999");
    }

    [Fact]
    public async Task PurchaseEntryViewModel_ScanBarcode_AddsAndIncrementsProductLine()
    {
        // Arrange
        var mockPurchaseService = new Mock<IPurchaseService>();
        var mockSupplierService = new Mock<ISupplierService>();
        var mockPurchaseCalculator = new Mock<IPurchaseCalculator>();

        mockPurchaseCalculator
            .Setup(c => c.CalculateLine(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns<decimal, decimal, decimal, decimal>((qty, cost, disc, tax) => PurchaseCalculator.CalculateLine(qty, cost, disc, tax));

        mockPurchaseCalculator
            .Setup(c => c.CalculateTotals(It.IsAny<IEnumerable<LineCalculationResult>>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns<IEnumerable<LineCalculationResult>, decimal, decimal>((lines, invDisc, freight) => PurchaseCalculator.CalculateTotals(lines, invDisc, freight));

        var pagedResult = new PagedResult<ProductListItemDto>
        {
            Items = new List<ProductListItemDto>
            {
                new()
                {
                    Id = 101,
                    Name = "Coca Cola 500ml",
                    Sku = "P000001",
                    Barcode = "8901030000018",
                    CostPrice = 55.00m,
                    SalePrice = 75.00m,
                    TaxPercent = 0m
                }
            },
            TotalCount = 1
        };

        _productService.Setup(p => p.GetPagedProductsAsync(It.IsAny<ProductSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<ProductListItemDto>>.Ok(pagedResult));

        mockSupplierService.Setup(s => s.GetFlatListAsync(false, "Owner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<SupplierDto>>.Ok(new List<SupplierDto>()));

        var vm = new PurchaseEntryViewModel(
            mockPurchaseService.Object,
            mockSupplierService.Object,
            _productService.Object,
            mockPurchaseCalculator.Object,
            _dialogService.Object);

        // Act 1: First scan
        vm.BarcodeInput = "8901030000018";
        await vm.AddScannedProductCommand.ExecuteAsync(null);

        // Assert 1
        vm.LineItems.Should().HaveCount(1);
        vm.LineItems[0].ProductId.Should().Be(101);
        vm.LineItems[0].Quantity.Should().Be(1m);

        // Act 2: Second scan of the same barcode
        vm.BarcodeInput = "8901030000018";
        await vm.AddScannedProductCommand.ExecuteAsync(null);

        // Assert 2
        vm.LineItems.Should().HaveCount(1);
        vm.LineItems[0].Quantity.Should().Be(2m);
    }

    [Fact]
    public async Task StockAdjustmentDialogViewModel_ScanBarcode_AddsAndIncrementsStockCount()
    {
        // Arrange
        var mockAdjustmentService = new Mock<IStockAdjustmentService>();

        var pagedResult = new PagedResult<ProductListItemDto>
        {
            Items = new List<ProductListItemDto>
            {
                new()
                {
                    Id = 101,
                    Name = "Coca Cola 500ml",
                    Sku = "P000001",
                    Barcode = "8901030000018",
                    CostPrice = 55.00m,
                    SalePrice = 75.00m
                }
            },
            TotalCount = 1
        };

        _productService.Setup(p => p.GetPagedProductsAsync(It.IsAny<ProductSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<ProductListItemDto>>.Ok(pagedResult));

        _stockLedgerQuery.Setup(s => s.GetCurrentStockAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15m);

        var vm = new StockAdjustmentDialogViewModel(
            null,
            mockAdjustmentService.Object,
            _productService.Object,
            _stockLedgerQuery.Object,
            _dialogService.Object);

        // Act 1: First scan
        vm.ScanInput = "8901030000018";
        await vm.ProcessScanInputCommand.ExecuteAsync(null);

        // Assert 1
        vm.Items.Should().HaveCount(1);
        vm.Items[0].ProductId.Should().Be(101);
        vm.Items[0].SystemQuantity.Should().Be(15m);
        vm.Items[0].NewQuantity.Should().Be(16m); // System + 1 on initial scan

        // Act 2: Repeat scan increments count
        vm.ScanInput = "8901030000018";
        await vm.ProcessScanInputCommand.ExecuteAsync(null);

        // Assert 2
        vm.Items.Should().HaveCount(1);
        vm.Items[0].NewQuantity.Should().Be(17m);
    }
}
