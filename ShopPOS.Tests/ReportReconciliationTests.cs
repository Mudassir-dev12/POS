using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.DTOs.Reports;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Data;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Repositories;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class ReportReconciliationTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private AppDbContext _context = null!;
    private ReportService _reportService = null!;
    private StockLedgerQuery _stockLedger = null!;
    private SupplierLedgerRepository _supplierLedgerRepo = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"shoppos_reconcile_{Guid.NewGuid():N}.db");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.MigrateAsync();

        _stockLedger = new StockLedgerQuery(_context);
        _supplierLedgerRepo = new SupplierLedgerRepository(_context);
        var settingRepo = new ShopSettingRepository(_context);
        var barcodeService = new BarcodeService(_context);
        _reportService = new ReportService(_context, settingRepo, barcodeService, _supplierLedgerRepo);

        await SeedReconciliationDataAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* Ignore */ }
        }
    }

    private async Task SeedReconciliationDataAsync()
    {
        // 1. Settings
        var setting = new ShopSetting
        {
            ShopName = "Test Shop",
            Address = "123 Test St",
            Phone = "03001234567",
            CurrencySymbol = "Rs",
            TaxNumber = "TAX-999"
        };
        _context.ShopSettings.Add(setting);

        // 2. Master Data
        var cat = new Category { Name = "Beverages" };
        var brand = new Brand { Name = "BrandA" };
        var unit = new Unit { Name = "Pcs", ShortName = "pcs" };
        _context.Categories.Add(cat);
        _context.Brands.Add(brand);
        _context.Units.Add(unit);
        await _context.SaveChangesAsync();

        var supplier = new Supplier { Name = "Supplier 1", Phone = "03001112223", OpeningBalance = 0m };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        // Products: P1 (Cost 100, Sale 150), P2 (Cost 200, Sale 300)
        var p1 = new Product
        {
            Name = "Product 1",
            Sku = "P1",
            Barcode = "2000000000017",
            CategoryId = cat.Id,
            BrandId = brand.Id,
            UnitId = unit.Id,
            CostPrice = 100m,
            SalePrice = 150m,
            TaxPercent = 0m
        };
        var p2 = new Product
        {
            Name = "Product 2",
            Sku = "P2",
            Barcode = "2000000000024",
            CategoryId = cat.Id,
            BrandId = brand.Id,
            UnitId = unit.Id,
            CostPrice = 200m,
            SalePrice = 300m,
            TaxPercent = 10m
        };
        _context.Products.AddRange(p1, p2);
        await _context.SaveChangesAsync();

        // 3. Purchase: 10 of P1 at 100 (Total 1000), 10 of P2 at 200 (Total 2000). Grand Total = 3000
        var purchaseDate = DateTime.Today.AddHours(6);
        var purchase = new Purchase
        {
            PurchaseNumber = "PUR-001",
            SupplierId = supplier.Id,
            PurchaseDate = purchaseDate,
            Status = PurchaseStatus.Posted,
            SubTotal = 3000m,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            GrandTotal = 3000m,
            PaidAmount = 1000m,
            Items = new List<PurchaseItem>
            {
                new() { ProductId = p1.Id, Quantity = 10, UnitCost = 100m, LineTotal = 1000m },
                new() { ProductId = p2.Id, Quantity = 10, UnitCost = 200m, LineTotal = 2000m }
            }
        };
        _context.Purchases.Add(purchase);
        await _context.SaveChangesAsync();

        // Stock movements for Purchase
        _context.StockMovements.Add(new StockMovement { ProductId = p1.Id, MovementType = StockMovementType.Purchase, Quantity = 10, UnitCost = 100m, MovementDate = purchaseDate, ReferenceType = "Purchase", ReferenceId = purchase.Id });
        _context.StockMovements.Add(new StockMovement { ProductId = p2.Id, MovementType = StockMovementType.Purchase, Quantity = 10, UnitCost = 200m, MovementDate = purchaseDate, ReferenceType = "Purchase", ReferenceId = purchase.Id });

        // Supplier payment of 1000
        var suppPay = new SupplierPayment
        {
            PaymentNumber = "SPAY-001",
            SupplierId = supplier.Id,
            PurchaseId = purchase.Id,
            Amount = 1000m,
            PaymentDate = purchaseDate,
            Method = PaymentMethod.Cash
        };
        _context.SupplierPayments.Add(suppPay);
        await _context.SaveChangesAsync();

        // 4. Completed Sale 1 (Today):
        // P1: qty 4 @ 150 = 600, Discount = 50 -> LineTotal = 550, CostSnapshot = 100
        // P2: qty 2 @ 300 = 600, Tax 10% on 600 = 60 -> LineTotal = 600, CostSnapshot = 200
        // Bill discount = 50, RoundOff = 0
        // SubTotal = 1200, LineDiscountTotal = 50, BillDiscountAmount = 50, TaxAmount = 60
        // GrandTotal = SubTotal - LineDiscountTotal - BillDiscountAmount + TaxAmount = 1200 - 50 - 50 + 60 = 1160
        // Payments: Cash 660, Card 500 (Total 1160)
        var today = DateTime.Today.AddHours(10);
        var sale1 = new Sale
        {
            SaleNumber = "INV-001",
            SaleDate = today,
            Status = SaleStatus.Completed,
            SubTotal = 1200m,
            LineDiscountTotal = 50m,
            BillDiscountAmount = 50m,
            TaxAmount = 60m,
            RoundOff = 0m,
            GrandTotal = 1160m,
            PaidAmount = 1160m,
            ChangeAmount = 0m,
            TotalCost = (4 * 100m) + (2 * 200m), // 800
            Items = new List<SaleItem>
            {
                new() { ProductId = p1.Id, Quantity = 4, UnitPrice = 150m, DiscountAmount = 50m, TaxPercent = 0m, LineTotal = 550m, CostPriceSnapshot = 100m, ProductName = p1.Name, Sku = p1.Sku },
                new() { ProductId = p2.Id, Quantity = 2, UnitPrice = 300m, DiscountAmount = 0m, TaxPercent = 10m, LineTotal = 600m, CostPriceSnapshot = 200m, ProductName = p2.Name, Sku = p2.Sku }
            },
            Payments = new List<SalePayment>
            {
                new() { Method = PaymentMethod.Cash, Amount = 660m, TenderedAmount = 660m, PaidAt = today },
                new() { Method = PaymentMethod.Card, Amount = 500m, TenderedAmount = 500m, PaidAt = today }
            }
        };
        _context.Sales.Add(sale1);
        _context.StockMovements.Add(new StockMovement { ProductId = p1.Id, MovementType = StockMovementType.Sale, Quantity = -4, UnitCost = 100m, MovementDate = today, ReferenceType = "Sale" });
        _context.StockMovements.Add(new StockMovement { ProductId = p2.Id, MovementType = StockMovementType.Sale, Quantity = -2, UnitCost = 200m, MovementDate = today, ReferenceType = "Sale" });
        await _context.SaveChangesAsync();

        // 5. Sale Return on INV-001 (Today):
        // 1 of P1 returned: sold at 150, discount share 12.50 -> SubTotal 137.50, Tax 0, ReturnToStock = true (cost 100 restored)
        // Refunded 137.50 in Cash
        var retDate = today.AddHours(2);
        var saleReturn = new SaleReturn
        {
            ReturnNumber = "SRET-001",
            SaleId = sale1.Id,
            ReturnDate = retDate,
            Status = SaleReturnStatus.Posted,
            SubTotal = 137.50m,
            TaxAmount = 0m,
            TotalAmount = 137.50m,
            RefundedAmount = 137.50m,
            RefundMethod = PaymentMethod.Cash,
            Items = new List<SaleReturnItem>
            {
                new() { SaleItemId = sale1.Items.First().Id, ProductId = p1.Id, Quantity = 1, UnitPrice = 150m, LineTotal = 137.50m, CostPriceSnapshot = 100m, ReturnToStock = true, ProductName = p1.Name, Sku = p1.Sku }
            }
        };
        _context.SaleReturns.Add(saleReturn);
        _context.StockMovements.Add(new StockMovement { ProductId = p1.Id, MovementType = StockMovementType.SaleReturn, Quantity = 1, UnitCost = 100m, MovementDate = retDate, ReferenceType = "SaleReturn" });

        // 6. Non-counting sales: 1 Draft/Held bill, 1 Cancelled sale
        var heldSale = new Sale { HoldNumber = "HOLD-001", Status = SaleStatus.Held, SubTotal = 500m, GrandTotal = 500m, SaleDate = today };
        var cancelledSale = new Sale { SaleNumber = "INV-CAN", Status = SaleStatus.Cancelled, SubTotal = 999m, GrandTotal = 999m, SaleDate = today, CancelReason = "Mistake" };
        _context.Sales.AddRange(heldSale, cancelledSale);

        // 7. Expenses: Category "Utilities", 1 active of 150 Cash, 1 cancelled of 300
        var expCat = new ExpenseCategory { Name = "Utilities" };
        _context.ExpenseCategories.Add(expCat);
        await _context.SaveChangesAsync();

        var exp1 = new Expense { ExpenseNumber = "EXP-001", ExpenseCategoryId = expCat.Id, Amount = 150m, ExpenseDate = today, Method = PaymentMethod.Cash, IsCancelled = false };
        var expCan = new Expense { ExpenseNumber = "EXP-CAN", ExpenseCategoryId = expCat.Id, Amount = 300m, ExpenseDate = today, Method = PaymentMethod.Cash, IsCancelled = true, CancelReason = "Duplicate" };
        _context.Expenses.AddRange(exp1, expCan);

        // 8. Stock Adjustment:
        // Reason Damaged: 1 of P1 (-1 @ 100 cost) -> Stock Write-off = 100
        // Reason Found: 2 of P2 (+2 @ 200 cost) -> Found Valuation = 400 (Informational, NOT added to profit)
        var adj1 = new StockAdjustment
        {
            AdjustmentNumber = "ADJ-001",
            Status = StockAdjustmentStatus.Posted,
            Reason = StockAdjustmentReason.Damaged,
            AdjustmentDate = today,
            Items = new List<StockAdjustmentItem>
            {
                new() { ProductId = p1.Id, SystemQuantity = 7, NewQuantity = 6, Difference = -1, UnitCost = 100m, ProductName = p1.Name, Sku = p1.Sku }
            }
        };
        var adj2 = new StockAdjustment
        {
            AdjustmentNumber = "ADJ-002",
            Status = StockAdjustmentStatus.Posted,
            Reason = StockAdjustmentReason.Found,
            AdjustmentDate = today,
            Items = new List<StockAdjustmentItem>
            {
                new() { ProductId = p2.Id, SystemQuantity = 8, NewQuantity = 10, Difference = 2, UnitCost = 200m, ProductName = p2.Name, Sku = p2.Sku }
            }
        };
        _context.StockAdjustments.AddRange(adj1, adj2);
        _context.StockMovements.Add(new StockMovement { ProductId = p1.Id, MovementType = StockMovementType.Adjustment, Quantity = -1, UnitCost = 100m, MovementDate = today, ReferenceType = "Adjustment" });
        _context.StockMovements.Add(new StockMovement { ProductId = p2.Id, MovementType = StockMovementType.Adjustment, Quantity = 2, UnitCost = 200m, MovementDate = today, ReferenceType = "Adjustment" });

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task Invariant1_DailySalesSummary_MatchesCompletedSales_NetEqualsGrossMinusDiscountsMinusReturns()
    {
        var range = DateRangeDto.FromPreset(ReportDatePreset.ThisMonth);
        var report = await _reportService.GetDailySalesSummaryAsync(range);

        report.TotalSalesCount.Should().Be(1);
        report.TotalGrossSales.Should().Be(1200m);
        report.TotalDiscounts.Should().Be(100m); // Line 50 + Bill 50
        report.TotalTax.Should().Be(60m);
        report.TotalReturns.Should().Be(137.50m);
        report.TotalNetSales.Should().Be(report.TotalGrossSales - report.TotalDiscounts - report.TotalReturns); // 1200 - 100 - 137.50 = 962.50m
    }

    [Fact]
    public async Task Invariant2_SalesByProduct_NetRevenue_Equals_PnL_NetSales_And_AllGroupingsMatch()
    {
        var range = DateRangeDto.FromPreset(ReportDatePreset.ThisMonth);

        var byProduct = await _reportService.GetSalesByProductAsync(range, SalesGroupBy.Product);
        var byCategory = await _reportService.GetSalesByProductAsync(range, SalesGroupBy.Category);
        var byBrand = await _reportService.GetSalesByProductAsync(range, SalesGroupBy.Brand);
        var byHour = await _reportService.GetSalesByProductAsync(range, SalesGroupBy.HourOfDay);
        var byMethod = await _reportService.GetSalesByProductAsync(range, SalesGroupBy.PaymentMethod);
        var pnl = await _reportService.GetProfitAndLossAsync(range);

        decimal pnlNetSales = pnl.CurrentNetSales;

        byProduct.TotalNetRevenue.Should().Be(pnlNetSales);
        byCategory.TotalNetRevenue.Should().Be(pnlNetSales);
        byBrand.TotalNetRevenue.Should().Be(pnlNetSales);
        byHour.TotalNetRevenue.Should().Be(pnlNetSales);
        byMethod.TotalNetRevenue.Should().Be(pnlNetSales);
    }

    [Fact]
    public async Task Invariant3_PaymentMethods_NetCashFlow_Matches_Sales_Minus_Refunds_Minus_Expenses_Minus_SupplierPayments()
    {
        var range = DateRangeDto.FromPreset(ReportDatePreset.ThisMonth);
        var report = await _reportService.GetPaymentMethodsReportAsync(range);

        var cashRow = report.Rows.FirstOrDefault(r => r.Method == PaymentMethod.Cash);
        cashRow.Should().NotBeNull();
        cashRow!.SalesCollected.Should().Be(660m);
        cashRow.RefundsPaid.Should().Be(137.50m);
        cashRow.ExpensesPaid.Should().Be(150m);
        cashRow.SupplierPaymentsPaid.Should().Be(1000m);

        decimal expectedNetCash = 660m - (137.50m + 150m + 1000m);
        cashRow.NetCashFlow.Should().Be(expectedNetCash);

        var cardRow = report.Rows.FirstOrDefault(r => r.Method == PaymentMethod.Card);
        cardRow.Should().NotBeNull();
        cardRow!.SalesCollected.Should().Be(500m);
        cardRow.NetCashFlow.Should().Be(500m);

        report.GrandTotalIn.Should().Be(1160m);
    }

    [Fact]
    public async Task Invariant4_CostOfGoodsSold_RestockedReturnsOnly_And_GrossProfitMatchesNetSalesMinusCogs()
    {
        var range = DateRangeDto.FromPreset(ReportDatePreset.ThisMonth);
        var pnl = await _reportService.GetProfitAndLossAsync(range);

        // Sales cost: (4 * 100) + (2 * 200) = 800
        // Return cost restored to stock: 1 * 100 = 100
        // Expected COGS = 800 - 100 = 700
        pnl.CurrentCostOfSales.Should().Be(800m);
        pnl.CurrentCostOfReturnsRestocked.Should().Be(100m);
        pnl.CurrentCogs.Should().Be(700m);

        // Gross Profit = Net Sales (962.50) - COGS (700) = 262.50
        pnl.CurrentGrossProfit.Should().Be(pnl.CurrentNetSales - pnl.CurrentCogs);
        pnl.CurrentGrossProfit.Should().Be(262.50m);
    }

    [Fact]
    public async Task Invariant5_NetProfit_DeductsExpenses_And_DamagedExpiredLostWriteOffs_SeparatesFound()
    {
        var range = DateRangeDto.FromPreset(ReportDatePreset.ThisMonth);
        var pnl = await _reportService.GetProfitAndLossAsync(range);

        // Operating Expenses = 150 (cancelled 300 excluded)
        pnl.CurrentTotalExpenses.Should().Be(150m);

        // Stock Write-offs = Damaged 1 * 100 = 100
        pnl.CurrentStockWriteOffs.Should().Be(100m);

        // Found Stock = 2 * 200 = 400 (NOT added to profit)
        pnl.CurrentFoundStockValuation.Should().Be(400m);

        // Net Profit = Gross Profit (262.50) - Expenses (150) - WriteOffs (100) = 12.50
        pnl.CurrentNetProfit.Should().Be(pnl.CurrentGrossProfit - pnl.CurrentTotalExpenses - pnl.CurrentStockWriteOffs);
        pnl.CurrentNetProfit.Should().Be(12.50m);
    }

    [Fact]
    public async Task Invariant6_StockMovementClosing_MatchesLedgerStock()
    {
        var range = DateRangeDto.FromPreset(ReportDatePreset.ThisMonth);
        var report = await _reportService.GetStockMovementSummaryAsync(range);

        var p1Move = report.MovementSummaryRows.FirstOrDefault(r => r.Sku == "P1");
        p1Move.Should().NotBeNull();
        // P1: Purchase 10, Sale 4, Return 1, Adjustment -1 -> Closing 6
        p1Move!.PurchasesIn.Should().Be(10);
        p1Move.SalesOut.Should().Be(4);
        p1Move.ReturnsIn.Should().Be(1);
        p1Move.AdjustmentsNet.Should().Be(-1);
        p1Move.ClosingStock.Should().Be(6);

        var ledgerStockP1 = await _stockLedger.GetCurrentStockAsync(p1Move.ProductId);
        p1Move.ClosingStock.Should().Be(ledgerStockP1);

        var p2Move = report.MovementSummaryRows.FirstOrDefault(r => r.Sku == "P2");
        p2Move.Should().NotBeNull();
        // P2: Purchase 10, Sale 2, Adjustment +2 -> Closing 10
        p2Move!.ClosingStock.Should().Be(10);
        var ledgerStockP2 = await _stockLedger.GetCurrentStockAsync(p2Move.ProductId);
        p2Move.ClosingStock.Should().Be(ledgerStockP2);
    }

    [Fact]
    public async Task Invariant7_PurchasesAndPayables_AgingBuckets_MatchSupplierBalances()
    {
        var range = DateRangeDto.FromPreset(ReportDatePreset.ThisMonth);
        var purchases = await _reportService.GetPurchasesReportAsync(range);

        purchases.TotalNetPurchases.Should().Be(3000m);
        purchases.TotalPaid.Should().Be(1000m);
        purchases.TotalPayablesBalance.Should().Be(2000m);

        var supplierRow = purchases.BySupplierRows.FirstOrDefault(s => s.SupplierName == "Supplier 1");
        supplierRow.Should().NotBeNull();
        supplierRow!.CurrentBalance.Should().Be(2000m);

        var agingRow = purchases.AgingRows.FirstOrDefault(s => s.SupplierName == "Supplier 1");
        agingRow.Should().NotBeNull();
        agingRow!.TotalBalance.Should().Be(2000m);
        agingRow.CurrentBucket.Should().Be(2000m); // Within last 30 days
    }

    [Fact]
    public async Task Invariant8_DashboardSummary_Matches_UnderlyingReports()
    {
        var summary = await _reportService.GetDashboardSummaryAsync();
        var daily = await _reportService.GetDailySalesSummaryAsync(DateRangeDto.FromPreset(ReportDatePreset.Today));
        var purchases = await _reportService.GetPurchasesReportAsync(DateRangeDto.FromPreset(ReportDatePreset.ThisYear));

        summary.TodayNetSales.Should().Be(daily.TotalNetSales);
        summary.TodaySalesCount.Should().Be(daily.TotalSalesCount);
        summary.HeldBillsCount.Should().Be(1); // 1 held bill created
        summary.SupplierPayables.Should().Be(purchases.TotalPayablesBalance);
        summary.Top5ProductsToday.Should().NotBeEmpty();
        summary.Last30DaysTrend.Should().HaveCount(30);
    }

    [Fact]
    public async Task Invariant9_EmptyPeriod_ReturnsZeroTotals_WithoutExceptions()
    {
        var pastRange = new DateRangeDto
        {
            StartDate = new DateTime(2020, 1, 1),
            EndDate = new DateTime(2020, 1, 31, 23, 59, 59, 999)
        };

        var daily = await _reportService.GetDailySalesSummaryAsync(pastRange);
        daily.Rows.Should().BeEmpty();
        daily.TotalSalesCount.Should().Be(0);
        daily.TotalNetSales.Should().Be(0m);
        daily.OverallAverageSaleValue.Should().Be(0m);

        var pnl = await _reportService.GetProfitAndLossAsync(pastRange);
        pnl.CurrentNetSales.Should().Be(0m);
        pnl.CurrentNetProfit.Should().Be(0m);

        var csv = _reportService.ExportReportToCsv("DailySalesSummary", daily);
        csv.Should().NotBeNull();
        csv.Length.Should().BeGreaterThan(3); // UTF-8 BOM present
    }

    [Fact]
    public async Task Invariant10_CsvAndPdfExports_FormatCorrectly()
    {
        var range = DateRangeDto.FromPreset(ReportDatePreset.ThisMonth);
        var daily = await _reportService.GetDailySalesSummaryAsync(range);

        // 1. CSV UTF-8 BOM check
        var csvBytes = _reportService.ExportReportToCsv("DailySalesSummary", daily);
        csvBytes.Should().NotBeNull();
        csvBytes.Length.Should().BeGreaterThan(3);
        csvBytes[0].Should().Be(0xEF);
        csvBytes[1].Should().Be(0xBB);
        csvBytes[2].Should().Be(0xBF);

        string csvContent = Encoding.UTF8.GetString(csvBytes);
        csvContent.Should().Contain("Date,Sales Count,Gross Sales");
        csvContent.Should().Contain("1200.00"); // Plain decimal numbers

        // 2. PDF Export QuestPDF check
        var setting = new ShopSettingDto { ShopName = "My Mart", Address = "Main St", Phone = "555-0100" };
        var pdfBytes = _reportService.ExportReportToPdf("DailySalesSummary", daily, setting);
        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(100);

        string header = Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
        header.Should().Be("%PDF");

        // 3. Render Images Check
        var images = _reportService.RenderReportPageImages("DailySalesSummary", daily, setting);
        images.Should().NotBeEmpty();
        images[0].Length.Should().BeGreaterThan(100);
    }
}
