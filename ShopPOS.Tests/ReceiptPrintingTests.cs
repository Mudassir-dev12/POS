using System.Text;
using AwesomeAssertions;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.Data.Services;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Sales;
using ShopPOS.UI.ViewModels.Settings;
using Xunit;

namespace ShopPOS.Tests;

public class ReceiptPrintingTests
{
    private readonly ShopSetting _defaultSetting;
    private readonly IReceiptBuilder _receiptBuilder = new ReceiptBuilder();

    public ReceiptPrintingTests()
    {
        _defaultSetting = new ShopSetting
        {
            Id = 1,
            ShopName = "Corner Grocery Store",
            Address = "742 Evergreen Terrace",
            Phone = "555-0199",
            TaxNumber = "VAT-98765432",
            CurrencySymbol = "Rs",
            ReceiptFooter = "Thank you for shopping local!",
            ReceiptWidthMm = 80,
            PrinterMode = PrinterMode.PdfPreview,
            ReceiptPrinterName = "POS-80-Printer",
            AutoPrintReceipt = true,
            OpenCashDrawerOnCashSale = true,
            ReceiptCopies = 1
        };
    }

    private static SaleDetailDto CreateSampleSale(bool withDue = false, bool isCredit = false)
    {
        return new SaleDetailDto
        {
            Id = 42,
            SaleNumber = "INV-2026-000042",
            SaleDate = new DateTime(2026, 11, 1, 14, 30, 0),
            SubTotal = 50.00m,
            LineDiscountTotal = 2.00m,
            BillDiscountAmount = 3.00m,
            TaxAmount = 4.50m,
            RoundOff = 0.00m,
            GrandTotal = 49.50m,
            PaidAmount = withDue ? 30.00m : 50.00m,
            ChangeAmount = withDue ? 0.00m : 0.50m,
            DueAmount = withDue ? 19.50m : 0.00m,
            Items = new List<SaleItemDetailDto>
            {
                new()
                {
                    Id = 1,
                    ProductId = 101,
                    ProductName = "Super Delicious Chocolate Frosted Donut with Sprinkles Deluxe",
                    Sku = "DON-001",
                    Quantity = 4m,
                    UnitPrice = 10.00m,
                    DiscountAmount = 2.00m,
                    DiscountPercent = 5.0m,
                    TaxPercent = 10.0m,
                    LineTotal = 38.00m
                },
                new()
                {
                    Id = 2,
                    ProductId = 102,
                    ProductName = "Duff Energy Drink 250ml",
                    Sku = "DUFF-250",
                    Quantity = 2m,
                    UnitPrice = 5.00m,
                    DiscountAmount = 0m,
                    DiscountPercent = 0m,
                    TaxPercent = 10.0m,
                    LineTotal = 10.00m
                }
            },
            Payments = new List<SalePaymentDetailDto>
            {
                new()
                {
                    Id = 1,
                    Method = PaymentMethod.Cash,
                    Amount = withDue ? 30.00m : 49.50m,
                    TenderedAmount = withDue ? 30.00m : 50.00m,
                    PaidAt = new DateTime(2026, 11, 1, 14, 30, 0)
                }
            }
        };
    }

    [Fact]
    public void ReceiptBuilder_TotalsMustMatchSaleExactly()
    {
        // Arrange
        var sale = CreateSampleSale();

        // Act
        var doc = _receiptBuilder.BuildFromSale(sale, _defaultSetting);

        // Assert
        doc.DocumentTitle.Should().Be("TAX INVOICE"); // because TaxNumber is set
        doc.ShopName.Should().Be("Corner Grocery Store");
        doc.InvoiceNumber.Should().Be("INV-2026-000042");
        doc.SubTotal.Should().Be(50.00m);
        doc.LineDiscountTotal.Should().Be(2.00m);
        doc.BillDiscountAmount.Should().Be(3.00m);
        doc.TotalDiscount.Should().Be(5.00m);
        doc.TaxAmount.Should().Be(4.50m);
        doc.RoundOff.Should().Be(0.00m);
        doc.GrandTotal.Should().Be(49.50m);
        doc.PaidAmount.Should().Be(50.00m);
        doc.ChangeAmount.Should().Be(0.50m);
        doc.DueAmount.Should().Be(0.00m);
        doc.Lines.Should().HaveCount(2);
        doc.Payments.Should().HaveCount(1);
        doc.IsDuplicateCopy.Should().BeFalse();
    }

    [Fact]
    public void ReceiptBuilder_DuplicateMarking_ShouldSetDuplicateProperties()
    {
        // Arrange
        var sale = CreateSampleSale();

        // Act
        var doc = _receiptBuilder.BuildFromSale(sale, _defaultSetting, isDuplicate: true, reprintCount: 2);

        // Assert
        doc.IsDuplicateCopy.Should().BeTrue();
        doc.ReprintCount.Should().Be(2);
    }

    
    [Fact]
    public void EscPosGenerator_WordWrap_ShouldBreakLongNamesCleanly()
    {
        // Arrange
        string longName = "Super Delicious Chocolate Frosted Donut with Sprinkles Deluxe";

        // Act for 58mm (32 chars)
        var lines58 = EscPosGenerator.WordWrap(longName, 32);

        // Assert
        lines58.Count.Should().BeGreaterThan(1);
        foreach (var line in lines58)
        {
            line.Length.Should().BeLessThanOrEqualTo(32);
        }

        // Act for 80mm (48 chars)
        var lines80 = EscPosGenerator.WordWrap(longName, 48);
        lines80.Count.Should().BeGreaterThan(1);
        foreach (var line in lines80)
        {
            line.Length.Should().BeLessThanOrEqualTo(48);
        }
    }

    [Fact]
    public void EscPosGenerator_Justify_ShouldProduceExactColumnWidth()
    {
        // Act
        string line32 = EscPosGenerator.Justify("Subtotal:", "Rs 49.50", 32);
        string line48 = EscPosGenerator.Justify("GRAND TOTAL:", "Rs 49.50", 48);

        // Assert
        line32.Length.Should().Be(32);
        line32.Should().StartWith("Subtotal:");
        line32.Should().EndWith("Rs 49.50");

        line48.Length.Should().Be(48);
        line48.Should().StartWith("GRAND TOTAL:");
        line48.Should().EndWith("Rs 49.50");
    }

    [Fact]
    public void EscPosGenerator_BytesMustContainInitBoldCutAndDrawerPulse()
    {
        // Arrange
        var sale = CreateSampleSale();
        var doc = _receiptBuilder.BuildFromSale(sale, _defaultSetting);

        // Act: Generate with cash drawer pulse
        byte[] bytes = EscPosGenerator.GenerateReceiptBytes(doc, kickDrawer: true);

        // Assert bytes are produced
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(100);

        // 1. Init command at start (ESC @ = 0x1B, 0x40)
        bytes[0].Should().Be(0x1B);
        bytes[1].Should().Be(0x40);

        // 2. Bold command present (ESC E 1 = 0x1B, 0x45, 0x01)
        ContainsSequence(bytes, [0x1B, 0x45, 0x01]).Should().BeTrue();

        // 3. Cut command present (GS V = 0x1D, 0x56)
        ContainsSequence(bytes, [0x1D, 0x56]).Should().BeTrue();

        // 4. Drawer pulse present (ESC p = 0x1B, 0x70)
        ContainsSequence(bytes, [0x1B, 0x70, 0x00, 0x19, 0xFA]).Should().BeTrue();
    }

    [Fact]
    public void EscPosGenerator_WithoutCashKick_MustNotContainDrawerPulse()
    {
        // Arrange
        var sale = CreateSampleSale();
        var doc = _receiptBuilder.BuildFromSale(sale, _defaultSetting);

        // Act: Generate without cash drawer pulse
        byte[] bytes = EscPosGenerator.GenerateReceiptBytes(doc, kickDrawer: false);

        // Assert: Drawer pulse command should NOT be present
        ContainsSequence(bytes, [0x1B, 0x70, 0x00, 0x19, 0xFA]).Should().BeFalse();
    }

    [Fact]
    public void EscPosGenerator_DuplicateCopy_MustIncludeDuplicateBanner()
    {
        // Arrange
        var sale = CreateSampleSale();
        var doc = _receiptBuilder.BuildFromSale(sale, _defaultSetting, isDuplicate: true, reprintCount: 1);

        // Act
        byte[] bytes = EscPosGenerator.GenerateReceiptBytes(doc, kickDrawer: false);
        string text = Encoding.ASCII.GetString(bytes);

        // Assert
        text.Should().Contain("*** DUPLICATE COPY ***");
        text.Should().Contain("Reprint #1");
    }

    [Fact]
    public async Task PdfReportService_GenerateReceiptPdf_ShouldProduceNonEmptyBytesForBothWidths()
    {
        // Arrange
        var pdfService = new PdfReportService();
        var sale = CreateSampleSale();

        // 1. Test 80mm PDF
        _defaultSetting.ReceiptWidthMm = 80;
        var doc80 = _receiptBuilder.BuildFromSale(sale, _defaultSetting);
        byte[] pdf80 = await pdfService.GenerateReceiptPdfAsync(doc80);

        pdf80.Should().NotBeNull();
        pdf80.Length.Should().BeGreaterThan(500);

        // 2. Test 58mm PDF
        _defaultSetting.ReceiptWidthMm = 58;
        var doc58 = _receiptBuilder.BuildFromSale(sale, _defaultSetting);
        byte[] pdf58 = await pdfService.GenerateReceiptPdfAsync(doc58);

        pdf58.Should().NotBeNull();
        pdf58.Length.Should().BeGreaterThan(500);

        // 3. Test A4 Corporate Invoice PDF
        byte[] pdfA4 = await pdfService.GenerateA4InvoicePdfAsync(doc80);
        pdfA4.Should().NotBeNull();
        pdfA4.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task ReceiptPrinterService_PrintReceipt_PdfPreviewMode_ReturnsPdfBytes()
    {
        // Arrange
        var pdfService = new PdfReportService();
        var settingService = new Mock<IShopSettingService>();
        settingService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(_defaultSetting);

        var service = new ReceiptPrinterService(pdfService, _receiptBuilder, settingService.Object);
        var sale = CreateSampleSale();
        var doc = _receiptBuilder.BuildFromSale(sale, _defaultSetting);

        _defaultSetting.PrinterMode = PrinterMode.PdfPreview;

        // Act
        var result = await service.PrintReceiptAsync(doc, _defaultSetting);

        // Assert
        result.Success.Should().BeTrue();
        result.PdfBytes.Should().NotBeNull();
        result.PdfBytes!.Length.Should().BeGreaterThan(500);
    }

    [Fact]
    public async Task ReceiptPrinterService_PrintReceipt_MissingPrinter_ReturnsFailureWithFriendlyMessage()
    {
        // Arrange
        var pdfService = new PdfReportService();
        var settingService = new Mock<IShopSettingService>();
        settingService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(_defaultSetting);

        var service = new ReceiptPrinterService(pdfService, _receiptBuilder, settingService.Object);
        var sale = CreateSampleSale();
        var doc = _receiptBuilder.BuildFromSale(sale, _defaultSetting);

        // Set missing printer name in Windows driver mode
        _defaultSetting.PrinterMode = PrinterMode.WindowsDriver;
        _defaultSetting.ReceiptPrinterName = "NonExistentPrinter_XYZ_999";

        // Act
        var result = await service.PrintReceiptAsync(doc, _defaultSetting);

        // Assert: MUST return Failure without throwing an exception!
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PosSaleViewModel_PrintFailure_LeavesSaleSavedAndShowsNonBlockingNotice()
    {
        // Arrange
        var mockSaleService = new Mock<ISaleService>();
        var mockPrinterService = new Mock<IReceiptPrinterService>();
        var mockDialogService = new Mock<IDialogService>();
        var mockShopSettingService = new Mock<IShopSettingService>();

        mockShopSettingService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(_defaultSetting);
        mockPrinterService.Setup(p => p.PrintSaleReceiptAsync(It.IsAny<SaleDetailDto>(), It.IsAny<ShopSetting>(), false, 0, default))
            .ReturnsAsync(ReceiptPrintResult.Fail("Printer offline."));

        var vm = new PosSaleViewModel(
            mockSaleService.Object,
            new SaleCalculator(),
            new Mock<IProductLookupService>().Object,
            new Mock<IProductService>().Object,
            new Mock<IPriceLevelService>().Object,
            new Mock<IStockLedgerQuery>().Object,
            mockShopSettingService.Object,
            mockDialogService.Object,
            new Mock<INavigationService>().Object,
            mockPrinterService.Object);

        vm.LastCompletedSale = CreateSampleSale();

        // Act: Execute print off-UI thread
        await vm.PrintLastReceiptAsync();

        // Assert: Non-blocking notice shown, no exception thrown
        mockDialogService.Verify(d => d.ShowMessage(
            It.Is<string>(msg => msg.Contains("Printer offline") && msg.IndexOf("reprint anytime", StringComparison.OrdinalIgnoreCase) >= 0),
            "Printer Notice"), Times.Once);
    }

    [Fact]
    public async Task SaleDetailDialogViewModel_ReprintReceipt_LogsAuditEntryAndMarksDuplicate()
    {
        // Arrange
        var mockSaleService = new Mock<ISaleService>();
        var mockPrinterService = new Mock<IReceiptPrinterService>();
        var mockAuditLogService = new Mock<IAuditLogService>();
        var mockCurrentUserProvider = new Mock<ICurrentUserProvider>();
        var mockShopSettingService = new Mock<IShopSettingService>();
        var mockDialogService = new Mock<IDialogService>();

        mockCurrentUserProvider.Setup(u => u.UserId).Returns(1);
        mockShopSettingService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(_defaultSetting);

        var sale = CreateSampleSale();
        mockSaleService.Setup(s => s.GetSaleByIdAsync(42, default))
            .ReturnsAsync(Result<SaleDetailDto>.Ok(sale));

        mockPrinterService.Setup(p => p.PrintSaleReceiptAsync(It.IsAny<SaleDetailDto>(), It.IsAny<ShopSetting>(), true, 0, default))
            .ReturnsAsync(ReceiptPrintResult.Ok(notice: "Duplicate receipt sent."));

        var vm = new SaleDetailDialogViewModel(
            42,
            mockSaleService.Object,
            new PdfReportService(),
            mockShopSettingService.Object,
            mockPrinterService.Object,
            _receiptBuilder,
            mockAuditLogService.Object,
            mockCurrentUserProvider.Object,
            mockDialogService.Object);

        await vm.LoadSaleAsync(42);

        // Act
        await vm.ReprintReceiptAsync();

        // Assert: Audit log recorded with action "ReprintReceipt"
        mockAuditLogService.Verify(a => a.LogAsync(
            1,
            "ReprintReceipt",
            "Sale",
            42,
            It.Is<string>(d => d.Contains("DUPLICATE"))), Times.Once);

        // Verify printer service called with isDuplicate: true
        mockPrinterService.Verify(p => p.PrintSaleReceiptAsync(
            It.Is<SaleDetailDto>(s => s.Id == 42),
            It.IsAny<ShopSetting>(),
            true,
            0,
            default), Times.Once);
    }

    [Fact]
    public async Task PrinterSettingsDialogViewModel_ValidationAndHardwareActions()
    {
        // Arrange
        var mockSettingService = new Mock<IShopSettingService>();
        var mockPrinterService = new Mock<IReceiptPrinterService>();

        mockSettingService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(_defaultSetting);
        mockPrinterService.Setup(p => p.GetInstalledPrintersAsync())
            .ReturnsAsync(new List<string> { "POS-80-Printer", "Microsoft Print to PDF" });

        mockPrinterService.Setup(p => p.PrintTestReceiptAsync(It.IsAny<ShopSetting>(), default))
            .ReturnsAsync(ReceiptPrintResult.Ok(notice: "Test print OK!"));

        mockPrinterService.Setup(p => p.OpenCashDrawerAsync(It.IsAny<string>()))
            .ReturnsAsync(ReceiptPrintResult.Ok(notice: "Drawer kick sent!"));

        var vm = new PrinterSettingsDialogViewModel(mockSettingService.Object, mockPrinterService.Object);
        await vm.InitializeAsync();

        // Assert loaded state
        vm.InstalledPrinters.Should().Contain("POS-80-Printer");
        vm.SelectedPrinter.Should().Be("POS-80-Printer");
        vm.ReceiptWidthMm.Should().Be(80);

        // Test Print action
        await vm.TestPrintAsync();
        vm.StatusMessage.Should().Be("Test print OK!");
        vm.ErrorMessage.Should().BeNull();

        // Open Drawer action
        await vm.OpenCashDrawerAsync();
        vm.StatusMessage.Should().Be("Drawer kick sent!");
        vm.ErrorMessage.Should().BeNull();

        // Save action
        await vm.SaveAsync();
        vm.Confirmed.Should().BeTrue();
        mockSettingService.Verify(s => s.UpdateSettingsAsync(It.IsAny<ShopSetting>()), Times.Once);
    }

    private static bool ContainsSequence(byte[] source, byte[] sequence)
    {
        if (sequence.Length > source.Length) return false;

        for (int i = 0; i <= source.Length - sequence.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < sequence.Length; j++)
            {
                if (source[i + j] != sequence[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }

        return false;
    }
}
