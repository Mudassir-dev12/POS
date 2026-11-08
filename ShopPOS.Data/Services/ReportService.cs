using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.DTOs.Reports;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _context;
    private readonly IShopSettingRepository _settingRepo;
    private readonly IBarcodeService _barcodeService;
    private readonly ISupplierLedgerRepository _supplierLedgerRepo;

    public ReportService(
        AppDbContext context,
        IShopSettingRepository settingRepo,
        IBarcodeService barcodeService,
        ISupplierLedgerRepository supplierLedgerRepo)
    {
        _context = context;
        _settingRepo = settingRepo;
        _barcodeService = barcodeService;
        _supplierLedgerRepo = supplierLedgerRepo;
    }

    // =========================================================================
    // 1. DAILY SALES SUMMARY
    // =========================================================================

    public async Task<DailySalesSummaryReportDto> GetDailySalesSummaryAsync(
        DateRangeDto dateRange,
        CancellationToken cancellationToken = default)
    {
        var report = new DailySalesSummaryReportDto { DateRange = dateRange };

        // 1. Completed sales in date range
        var sales = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Payments)
            .Where(s => !s.IsDeleted && s.Status == SaleStatus.Completed &&
                        s.SaleDate >= dateRange.StartDate && s.SaleDate <= dateRange.EndDate)
            .OrderBy(s => s.SaleDate)
            .ToListAsync(cancellationToken);

        // 2. Posted sale returns in date range
        var returns = await _context.SaleReturns
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.Status == SaleReturnStatus.Posted &&
                        r.ReturnDate >= dateRange.StartDate && r.ReturnDate <= dateRange.EndDate)
            .OrderBy(r => r.ReturnDate)
            .ToListAsync(cancellationToken);

        // Group by Date
        var allDates = sales.Select(s => s.SaleDate.Date)
            .Union(returns.Select(r => r.ReturnDate.Date))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        foreach (var date in allDates)
        {
            var daySales = sales.Where(s => s.SaleDate.Date == date).ToList();
            var dayReturns = returns.Where(r => r.ReturnDate.Date == date).ToList();

            var row = new DailySalesSummaryRowDto
            {
                Date = date,
                SalesCount = daySales.Count,
                GrossSales = daySales.Sum(s => s.SubTotal),
                Discounts = daySales.Sum(s => s.LineDiscountTotal + s.BillDiscountAmount),
                Tax = daySales.Sum(s => s.TaxAmount) - dayReturns.Sum(r => r.TaxAmount),
                Returns = dayReturns.Sum(r => r.TotalAmount)
            };

            // Net sales = sale total net of tax minus return total net of tax
            decimal daySalesNet = daySales.Sum(s => s.GrandTotal - s.TaxAmount - s.RoundOff);
            decimal dayReturnsNet = dayReturns.Sum(r => r.SubTotal);
            row.NetSales = daySalesNet - dayReturnsNet;

            // Payment method splits: payments applied minus refunds paid on that day
            var dayPayments = daySales.SelectMany(s => s.Payments).ToList();
            row.CashAmount = dayPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount) -
                             dayReturns.Where(r => r.RefundMethod == PaymentMethod.Cash).Sum(r => r.RefundedAmount);

            row.CardAmount = dayPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount) -
                             dayReturns.Where(r => r.RefundMethod == PaymentMethod.Card).Sum(r => r.RefundedAmount);

            row.BankAmount = dayPayments.Where(p => p.Method == PaymentMethod.Bank).Sum(p => p.Amount) -
                             dayReturns.Where(r => r.RefundMethod == PaymentMethod.Bank).Sum(r => r.RefundedAmount);

            row.MobileWalletAmount = dayPayments.Where(p => p.Method == PaymentMethod.MobileWallet).Sum(p => p.Amount) -
                                     dayReturns.Where(r => r.RefundMethod == PaymentMethod.MobileWallet).Sum(r => r.RefundedAmount);

            row.OtherAmount = dayPayments.Where(p => p.Method == PaymentMethod.Other || p.Method == PaymentMethod.Cheque).Sum(p => p.Amount) -
                              dayReturns.Where(r => r.RefundMethod == PaymentMethod.Other || r.RefundMethod == PaymentMethod.Cheque).Sum(r => r.RefundedAmount);

            report.Rows.Add(row);
        }

        // Summary totals
        report.TotalSalesCount = report.Rows.Sum(r => r.SalesCount);
        report.TotalGrossSales = report.Rows.Sum(r => r.GrossSales);
        report.TotalDiscounts = report.Rows.Sum(r => r.Discounts);
        report.TotalTax = report.Rows.Sum(r => r.Tax);
        report.TotalReturns = report.Rows.Sum(r => r.Returns);
        report.TotalNetSales = report.Rows.Sum(r => r.NetSales);
        report.TotalCash = report.Rows.Sum(r => r.CashAmount);
        report.TotalCard = report.Rows.Sum(r => r.CardAmount);
        report.TotalBank = report.Rows.Sum(r => r.BankAmount);
        report.TotalMobileWallet = report.Rows.Sum(r => r.MobileWalletAmount);
        report.TotalOther = report.Rows.Sum(r => r.OtherAmount);

        return report;
    }

    // =========================================================================
    // 2. SALES BY PRODUCT (With Grouping by Category, Brand, Hour, Method)
    // =========================================================================

    public async Task<SalesByProductReportDto> GetSalesByProductAsync(
        DateRangeDto dateRange,
        SalesGroupBy groupBy = SalesGroupBy.Product,
        int? topN = null,
        string? sortBy = null,
        bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        var report = new SalesByProductReportDto
        {
            DateRange = dateRange,
            GroupBy = groupBy
        };

        var saleItemsQuery = _context.SaleItems
            .AsNoTracking()
            .Where(si => !si.Sale!.IsDeleted && si.Sale.Status == SaleStatus.Completed &&
                         si.Sale.SaleDate >= dateRange.StartDate && si.Sale.SaleDate <= dateRange.EndDate);

        var returnItemsQuery = _context.SaleReturnItems
            .AsNoTracking()
            .Where(ri => !ri.SaleReturn!.IsDeleted && ri.SaleReturn.Status == SaleReturnStatus.Posted &&
                         ri.SaleReturn.ReturnDate >= dateRange.StartDate && ri.SaleReturn.ReturnDate <= dateRange.EndDate);

        var settings = await _settingRepo.GetCurrentSettingsAsync(cancellationToken);
        bool pricesIncludeTax = settings?.PricesIncludeTax ?? false;

        if (groupBy == SalesGroupBy.Product)
        {
            var saleProductAggs = await saleItemsQuery
                .GroupBy(si => new { si.ProductId, si.ProductName, si.Sku, CategoryName = si.Product != null && si.Product.Category != null ? si.Product.Category.Name : "General", BrandName = si.Product != null && si.Product.Brand != null ? si.Product.Brand.Name : "None" })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Key.Sku,
                    g.Key.CategoryName,
                    g.Key.BrandName,
                    QuantitySold = g.Sum(x => x.Quantity),
                    GrossRevenue = g.Sum(x => x.Quantity * x.UnitPrice),
                    DiscountAmount = g.Sum(x => x.DiscountAmount),
                    LineTotalSum = g.Sum(x => x.LineTotal),
                    Cost = g.Sum(x => x.Quantity * x.CostPriceSnapshot),
                    TaxRate = g.Max(x => x.TaxPercent)
                })
                .ToListAsync(cancellationToken);

            var completedSales = await _context.Sales
                .AsNoTracking()
                .Where(s => !s.IsDeleted && s.Status == SaleStatus.Completed &&
                            s.SaleDate >= dateRange.StartDate && s.SaleDate <= dateRange.EndDate)
                .Select(s => new { s.Id, s.SubTotal, s.LineDiscountTotal, s.BillDiscountAmount, s.GrandTotal, s.TaxAmount, s.RoundOff })
                .ToListAsync(cancellationToken);

            decimal totalCompletedSalesNet = completedSales.Sum(s => s.GrandTotal - s.TaxAmount - s.RoundOff);

            var returnProductAggs = await returnItemsQuery
                .GroupBy(ri => ri.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    QuantityReturned = g.Sum(x => x.Quantity),
                    ReturnAmount = g.Sum(x => x.LineTotal),
                    RestockedCostReversed = g.Where(x => x.ReturnToStock).Sum(x => x.Quantity * x.CostPriceSnapshot)
                })
                .ToListAsync(cancellationToken);

            var returnDict = returnProductAggs.ToDictionary(r => r.ProductId);

            var rows = new List<SalesByProductRowDto>();

            decimal rawLinesNetSum = saleProductAggs.Sum(s => pricesIncludeTax
                ? (s.TaxRate > 0 ? (s.LineTotalSum / (1m + s.TaxRate / 100m)) : s.LineTotalSum)
                : s.LineTotalSum);

            foreach (var s in saleProductAggs)
            {
                returnDict.TryGetValue(s.ProductId, out var ret);

                decimal productNetBeforeBillDisc = pricesIncludeTax
                    ? (s.TaxRate > 0 ? (s.LineTotalSum / (1m + s.TaxRate / 100m)) : s.LineTotalSum)
                    : s.LineTotalSum;

                decimal productNetRevenue = rawLinesNetSum > 0
                    ? Math.Round(totalCompletedSalesNet * (productNetBeforeBillDisc / rawLinesNetSum), 2)
                    : productNetBeforeBillDisc;

                decimal retAmount = ret?.ReturnAmount ?? 0m;
                decimal costReversed = ret?.RestockedCostReversed ?? 0m;

                rows.Add(new SalesByProductRowDto
                {
                    ProductId = s.ProductId,
                    GroupKey = s.Sku,
                    Name = s.ProductName,
                    Sku = s.Sku,
                    CategoryName = s.CategoryName,
                    BrandName = s.BrandName,
                    QuantitySold = s.QuantitySold - (ret?.QuantityReturned ?? 0m),
                    GrossRevenue = s.GrossRevenue,
                    DiscountAmount = s.DiscountAmount,
                    ReturnAmount = retAmount,
                    NetRevenue = productNetRevenue - retAmount,
                    Cost = s.Cost - costReversed
                });
            }

            var saleProductIds = new HashSet<int>(saleProductAggs.Select(s => s.ProductId));
            foreach (var ret in returnProductAggs.Where(r => !saleProductIds.Contains(r.ProductId)))
            {
                var prod = await _context.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Brand).FirstOrDefaultAsync(p => p.Id == ret.ProductId, cancellationToken);
                rows.Add(new SalesByProductRowDto
                {
                    ProductId = ret.ProductId,
                    GroupKey = prod?.Sku ?? $"P{ret.ProductId}",
                    Name = prod?.Name ?? "Returned Product",
                    Sku = prod?.Sku ?? "",
                    CategoryName = prod?.Category?.Name ?? "General",
                    BrandName = prod?.Brand?.Name ?? "None",
                    QuantitySold = -ret.QuantityReturned,
                    GrossRevenue = 0m,
                    DiscountAmount = 0m,
                    ReturnAmount = ret.ReturnAmount,
                    NetRevenue = -ret.ReturnAmount,
                    Cost = -ret.RestockedCostReversed
                });
            }

            decimal totalReturnsNet = await _context.SaleReturns
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.Status == SaleReturnStatus.Posted && r.ReturnDate >= dateRange.StartDate && r.ReturnDate <= dateRange.EndDate)
                .SumAsync(r => r.SubTotal, cancellationToken);

            decimal targetNetRevenue = totalCompletedSalesNet - totalReturnsNet;
            decimal currentNetSum = rows.Sum(r => r.NetRevenue);
            if (rows.Count > 0 && currentNetSum != targetNetRevenue)
            {
                rows[0].NetRevenue += (targetNetRevenue - currentNetSum);
            }

            report.Rows = rows;
        }
        else if (groupBy == SalesGroupBy.Category || groupBy == SalesGroupBy.Brand)
        {
            var prodReport = await GetSalesByProductAsync(dateRange, SalesGroupBy.Product, null, null, true, cancellationToken);

            var grouped = groupBy == SalesGroupBy.Category
                ? prodReport.Rows.GroupBy(r => string.IsNullOrWhiteSpace(r.CategoryName) ? "Unassigned" : r.CategoryName)
                : prodReport.Rows.GroupBy(r => string.IsNullOrWhiteSpace(r.BrandName) ? "Unassigned" : r.BrandName);

            report.Rows = grouped.Select(g => new SalesByProductRowDto
            {
                GroupKey = g.Key,
                Name = g.Key,
                QuantitySold = g.Sum(x => x.QuantitySold),
                GrossRevenue = g.Sum(x => x.GrossRevenue),
                DiscountAmount = g.Sum(x => x.DiscountAmount),
                ReturnAmount = g.Sum(x => x.ReturnAmount),
                NetRevenue = g.Sum(x => x.NetRevenue),
                Cost = g.Sum(x => x.Cost)
            }).ToList();
        }
        else if (groupBy == SalesGroupBy.HourOfDay)
        {
            var completedSales = await _context.Sales
                .AsNoTracking()
                .Where(s => !s.IsDeleted && s.Status == SaleStatus.Completed &&
                            s.SaleDate >= dateRange.StartDate && s.SaleDate <= dateRange.EndDate)
                .ToListAsync(cancellationToken);

            var completedReturns = await _context.SaleReturns
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.Status == SaleReturnStatus.Posted &&
                            r.ReturnDate >= dateRange.StartDate && r.ReturnDate <= dateRange.EndDate)
                .ToListAsync(cancellationToken);

            var saleItems = await saleItemsQuery.ToListAsync(cancellationToken);

            var rows = new List<SalesByProductRowDto>();
            for (int h = 0; h < 24; h++)
            {
                var hourSales = completedSales.Where(s => s.SaleDate.Hour == h).ToList();
                var hourReturns = completedReturns.Where(r => r.ReturnDate.Hour == h).ToList();
                var hourSaleIds = new HashSet<int>(hourSales.Select(s => s.Id));
                var hourItems = saleItems.Where(si => hourSaleIds.Contains(si.SaleId)).ToList();

                decimal netSales = hourSales.Sum(s => s.GrandTotal - s.TaxAmount - s.RoundOff) - hourReturns.Sum(r => r.SubTotal);
                decimal cost = hourItems.Sum(i => i.Quantity * i.CostPriceSnapshot);

                if (hourSales.Count > 0 || hourReturns.Count > 0)
                {
                    rows.Add(new SalesByProductRowDto
                    {
                        GroupKey = $"{h:00}:00 - {h:00}:59",
                        Name = $"{h:00}:00 - {h:00}:59",
                        QuantitySold = hourItems.Sum(i => i.Quantity),
                        GrossRevenue = hourSales.Sum(s => s.SubTotal),
                        DiscountAmount = hourSales.Sum(s => s.LineDiscountTotal + s.BillDiscountAmount),
                        ReturnAmount = hourReturns.Sum(r => r.TotalAmount),
                        NetRevenue = netSales,
                        Cost = cost
                    });
                }
            }
            report.Rows = rows;
        }
        else if (groupBy == SalesGroupBy.PaymentMethod)
        {
            var completedSales = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Payments)
                .Where(s => !s.IsDeleted && s.Status == SaleStatus.Completed &&
                            s.SaleDate >= dateRange.StartDate && s.SaleDate <= dateRange.EndDate)
                .ToListAsync(cancellationToken);

            var completedReturns = await _context.SaleReturns
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.Status == SaleReturnStatus.Posted &&
                            r.ReturnDate >= dateRange.StartDate && r.ReturnDate <= dateRange.EndDate)
                .ToListAsync(cancellationToken);

            var methods = Enum.GetValues<PaymentMethod>();
            var rows = new List<SalesByProductRowDto>();

            foreach (var method in methods)
            {
                decimal gross = 0m;
                decimal net = 0m;

                foreach (var sale in completedSales)
                {
                    decimal methodPaid = sale.Payments.Where(p => p.Method == method).Sum(p => p.Amount);
                    if (methodPaid > 0)
                    {
                        gross += methodPaid;
                        decimal saleNet = sale.GrandTotal - sale.TaxAmount - sale.RoundOff;
                        decimal ratio = sale.GrandTotal > 0 ? (saleNet / sale.GrandTotal) : 1m;
                        net += methodPaid * ratio;
                    }
                }

                decimal refunds = 0m;
                foreach (var ret in completedReturns)
                {
                    if (ret.RefundMethod == method)
                    {
                        refunds += ret.RefundedAmount;
                        decimal retNet = ret.SubTotal;
                        decimal ratio = ret.TotalAmount > 0 ? (retNet / ret.TotalAmount) : 1m;
                        net -= ret.RefundedAmount * ratio;
                    }
                }

                if (gross > 0 || refunds > 0)
                {
                    rows.Add(new SalesByProductRowDto
                    {
                        GroupKey = method.ToString(),
                        Name = method.ToString(),
                        QuantitySold = 0m,
                        GrossRevenue = gross,
                        DiscountAmount = 0m,
                        ReturnAmount = refunds,
                        NetRevenue = Math.Round(net, 2, MidpointRounding.AwayFromZero),
                        Cost = 0m
                    });
                }
            }

            decimal targetNetRevenue = completedSales.Sum(s => s.GrandTotal - s.TaxAmount - s.RoundOff) - completedReturns.Sum(r => r.SubTotal);
            decimal currentNetSum = rows.Sum(r => r.NetRevenue);
            if (rows.Count > 0 && Math.Abs(targetNetRevenue - currentNetSum) > 0.0001m)
            {
                rows[0].NetRevenue += (targetNetRevenue - currentNetSum);
            }

            report.Rows = rows;
        }

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            report.Rows = sortBy.ToLowerInvariant() switch
            {
                "quantity" => sortDescending ? report.Rows.OrderByDescending(r => r.QuantitySold).ToList() : report.Rows.OrderBy(r => r.QuantitySold).ToList(),
                "cost" => sortDescending ? report.Rows.OrderByDescending(r => r.Cost).ToList() : report.Rows.OrderBy(r => r.Cost).ToList(),
                "profit" => sortDescending ? report.Rows.OrderByDescending(r => r.Profit).ToList() : report.Rows.OrderBy(r => r.Profit).ToList(),
                "margin" => sortDescending ? report.Rows.OrderByDescending(r => r.MarginPercent).ToList() : report.Rows.OrderBy(r => r.MarginPercent).ToList(),
                _ => sortDescending ? report.Rows.OrderByDescending(r => r.NetRevenue).ToList() : report.Rows.OrderBy(r => r.NetRevenue).ToList()
            };
        }
        else
        {
            report.Rows = report.Rows.OrderByDescending(r => r.NetRevenue).ToList();
        }

        if (topN.HasValue && topN.Value > 0)
        {
            report.Rows = report.Rows.Take(topN.Value).ToList();
        }

        report.TotalQuantitySold = report.Rows.Sum(r => r.QuantitySold);
        report.TotalNetRevenue = report.Rows.Sum(r => r.NetRevenue);
        report.TotalCost = report.Rows.Sum(r => r.Cost);

        return report;
    }

    // =========================================================================
    // 3. PROFIT AND LOSS
    // =========================================================================

    public async Task<ProfitAndLossReportDto> GetProfitAndLossAsync(
        DateRangeDto dateRange,
        CancellationToken cancellationToken = default)
    {
        var prevRange = dateRange.GetPreviousPeriod();

        var report = new ProfitAndLossReportDto
        {
            CurrentPeriod = dateRange,
            PreviousPeriod = prevRange
        };

        await PopulatePeriodPnlAsync(report, dateRange, isCurrent: true, cancellationToken);
        await PopulatePeriodPnlAsync(report, prevRange, isCurrent: false, cancellationToken);

        var currentExpenses = await _context.Expenses
            .AsNoTracking()
            .Include(e => e.ExpenseCategory)
            .Where(e => !e.IsCancelled && e.ExpenseDate >= dateRange.StartDate && e.ExpenseDate <= dateRange.EndDate)
            .GroupBy(e => new { e.ExpenseCategoryId, Name = e.ExpenseCategory != null ? e.ExpenseCategory.Name : "Other" })
            .Select(g => new { g.Key.ExpenseCategoryId, g.Key.Name, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var previousExpenses = await _context.Expenses
            .AsNoTracking()
            .Include(e => e.ExpenseCategory)
            .Where(e => !e.IsCancelled && e.ExpenseDate >= prevRange.StartDate && e.ExpenseDate <= prevRange.EndDate)
            .GroupBy(e => new { e.ExpenseCategoryId, Name = e.ExpenseCategory != null ? e.ExpenseCategory.Name : "Other" })
            .Select(g => new { g.Key.ExpenseCategoryId, g.Key.Name, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var allCatIds = currentExpenses.Select(e => e.ExpenseCategoryId)
            .Union(previousExpenses.Select(e => e.ExpenseCategoryId))
            .Distinct()
            .ToList();

        var expenseCategories = await _context.ExpenseCategories.AsNoTracking().ToListAsync(cancellationToken);
        var catNameDict = expenseCategories.ToDictionary(c => c.Id, c => c.Name);

        var expenseRows = new List<ExpenseByCategoryRowDto>();
        foreach (var catId in allCatIds)
        {
            decimal curAmt = currentExpenses.FirstOrDefault(e => e.ExpenseCategoryId == catId)?.Amount ?? 0m;
            decimal prevAmt = previousExpenses.FirstOrDefault(e => e.ExpenseCategoryId == catId)?.Amount ?? 0m;
            catNameDict.TryGetValue(catId, out var name);

            expenseRows.Add(new ExpenseByCategoryRowDto
            {
                CategoryId = catId,
                CategoryName = name ?? "Other",
                CurrentAmount = curAmt,
                PreviousAmount = prevAmt
            });
        }

        report.ExpensesByCategory = expenseRows.OrderByDescending(e => e.CurrentAmount).ToList();

        return report;
    }

    private async Task PopulatePeriodPnlAsync(
        ProfitAndLossReportDto report,
        DateRangeDto period,
        bool isCurrent,
        CancellationToken cancellationToken)
    {
        var sales = await _context.Sales
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.Status == SaleStatus.Completed &&
                        s.SaleDate >= period.StartDate && s.SaleDate <= period.EndDate)
            .ToListAsync(cancellationToken);

        var returns = await _context.SaleReturns
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.Status == SaleReturnStatus.Posted &&
                        r.ReturnDate >= period.StartDate && r.ReturnDate <= period.EndDate)
            .ToListAsync(cancellationToken);

        var saleItems = await _context.SaleItems
            .AsNoTracking()
            .Where(si => !si.Sale!.IsDeleted && si.Sale.Status == SaleStatus.Completed &&
                         si.Sale.SaleDate >= period.StartDate && si.Sale.SaleDate <= period.EndDate)
            .ToListAsync(cancellationToken);

        var returnItemsRestocked = await _context.SaleReturnItems
            .AsNoTracking()
            .Where(ri => !ri.SaleReturn!.IsDeleted && ri.SaleReturn.Status == SaleReturnStatus.Posted &&
                         ri.SaleReturn.ReturnDate >= period.StartDate && ri.SaleReturn.ReturnDate <= period.EndDate &&
                         ri.ReturnToStock)
            .ToListAsync(cancellationToken);

        decimal totalExpenses = await _context.Expenses
            .AsNoTracking()
            .Where(e => !e.IsCancelled && e.ExpenseDate >= period.StartDate && e.ExpenseDate <= period.EndDate)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var writeOffItems = await _context.StockAdjustmentItems
            .AsNoTracking()
            .Where(ai => !ai.StockAdjustment!.IsDeleted && ai.StockAdjustment.Status == StockAdjustmentStatus.Posted &&
                         ai.StockAdjustment.AdjustmentDate >= period.StartDate && ai.StockAdjustment.AdjustmentDate <= period.EndDate &&
                         (ai.StockAdjustment.Reason == StockAdjustmentReason.Damaged ||
                          ai.StockAdjustment.Reason == StockAdjustmentReason.Expired ||
                          ai.StockAdjustment.Reason == StockAdjustmentReason.Lost) &&
                         ai.Difference < 0)
            .ToListAsync(cancellationToken);

        decimal stockWriteOffs = writeOffItems.Sum(i => Math.Abs(i.Difference) * i.UnitCost);

        var foundItems = await _context.StockAdjustmentItems
            .AsNoTracking()
            .Where(ai => !ai.StockAdjustment!.IsDeleted && ai.StockAdjustment.Status == StockAdjustmentStatus.Posted &&
                         ai.StockAdjustment.AdjustmentDate >= period.StartDate && ai.StockAdjustment.AdjustmentDate <= period.EndDate &&
                         ai.StockAdjustment.Reason == StockAdjustmentReason.Found &&
                         ai.Difference > 0)
            .ToListAsync(cancellationToken);

        decimal foundStock = foundItems.Sum(i => i.Difference * i.UnitCost);

        decimal grossSales = sales.Sum(s => s.SubTotal);
        decimal salesDiscounts = sales.Sum(s => s.LineDiscountTotal + s.BillDiscountAmount);
        decimal salesReturns = returns.Sum(r => r.SubTotal);

        decimal costOfSales = saleItems.Sum(si => si.Quantity * si.CostPriceSnapshot);
        decimal costOfReturnsRestocked = returnItemsRestocked.Sum(ri => ri.Quantity * ri.CostPriceSnapshot);

        if (isCurrent)
        {
            report.CurrentGrossSales = grossSales;
            report.CurrentSalesDiscounts = salesDiscounts;
            report.CurrentSalesReturns = salesReturns;
            report.CurrentCostOfSales = costOfSales;
            report.CurrentCostOfReturnsRestocked = costOfReturnsRestocked;
            report.CurrentTotalExpenses = totalExpenses;
            report.CurrentStockWriteOffs = stockWriteOffs;
            report.CurrentFoundStockValuation = foundStock;
        }
        else
        {
            report.PreviousGrossSales = grossSales;
            report.PreviousSalesDiscounts = salesDiscounts;
            report.PreviousSalesReturns = salesReturns;
            report.PreviousCostOfSales = costOfSales;
            report.PreviousCostOfReturnsRestocked = costOfReturnsRestocked;
            report.PreviousTotalExpenses = totalExpenses;
            report.PreviousStockWriteOffs = stockWriteOffs;
            report.PreviousFoundStockValuation = foundStock;
        }
    }

    // =========================================================================
    // 4. PURCHASES REPORT & SUPPLIER AGING
    // =========================================================================

    public async Task<PurchasesReportDto> GetPurchasesReportAsync(
        DateRangeDto dateRange,
        CancellationToken cancellationToken = default)
    {
        var report = new PurchasesReportDto { DateRange = dateRange };

        var purchases = await _context.Purchases
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => !p.IsDeleted && p.Status == PurchaseStatus.Posted &&
                        p.PurchaseDate >= dateRange.StartDate && p.PurchaseDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var purchaseReturns = await _context.PurchaseReturns
            .AsNoTracking()
            .Where(pr => !pr.IsDeleted && pr.Status == PurchaseReturnStatus.Posted &&
                         pr.ReturnDate >= dateRange.StartDate && pr.ReturnDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var suppliers = await _context.Suppliers.AsNoTracking().Where(s => !s.IsDeleted).ToListAsync(cancellationToken);

        var supplierBalances = await _supplierLedgerRepo.GetSupplierBalancesAsync(suppliers.Select(s => s.Id), cancellationToken);

        var supplierGrouped = purchases.GroupBy(p => p.SupplierId).ToList();
        foreach (var g in supplierGrouped)
        {
            var sup = suppliers.FirstOrDefault(s => s.Id == g.Key);
            decimal returnsTotal = purchaseReturns.Where(pr => pr.SupplierId == g.Key).Sum(pr => pr.TotalAmount);
            supplierBalances.TryGetValue(g.Key, out decimal bal);

            report.BySupplierRows.Add(new PurchaseSupplierRowDto
            {
                SupplierId = g.Key,
                SupplierName = sup?.Name ?? "Supplier",
                Phone = sup?.Phone ?? "",
                InvoiceCount = g.Count(),
                SubTotal = g.Sum(x => x.SubTotal),
                DiscountAmount = g.Sum(x => x.DiscountAmount),
                TaxAmount = g.Sum(x => x.TaxAmount),
                FreightAmount = g.Sum(x => x.FreightAmount),
                GrandTotal = g.Sum(x => x.GrandTotal),
                ReturnsTotal = returnsTotal,
                PaidAmount = g.Sum(x => x.PaidAmount),
                CurrentBalance = bal
            });
        }

        var purchaseItems = await _context.PurchaseItems
            .AsNoTracking()
            .Where(pi => !pi.Purchase!.IsDeleted && pi.Purchase.Status == PurchaseStatus.Posted &&
                         pi.Purchase.PurchaseDate >= dateRange.StartDate && pi.Purchase.PurchaseDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var purchaseReturnItems = await _context.PurchaseReturnItems
            .AsNoTracking()
            .Where(pri => !pri.PurchaseReturn!.IsDeleted && pri.PurchaseReturn.Status == PurchaseReturnStatus.Posted &&
                          pri.PurchaseReturn.ReturnDate >= dateRange.StartDate && pri.PurchaseReturn.ReturnDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var productGrouped = purchaseItems.GroupBy(pi => new { pi.ProductId, pi.Sku, pi.ProductName }).ToList();
        foreach (var g in productGrouped)
        {
            var retItems = purchaseReturnItems.Where(pri => pri.ProductId == g.Key.ProductId).ToList();
            decimal retQty = retItems.Sum(x => x.Quantity);
            decimal retSpend = retItems.Sum(x => x.LineTotal);
            decimal totalCost = g.Sum(x => x.LineTotal);

            report.ByProductRows.Add(new PurchaseProductRowDto
            {
                ProductId = g.Key.ProductId,
                Sku = g.Key.Sku,
                ProductName = g.Key.ProductName,
                QuantityPurchased = g.Sum(x => x.Quantity),
                TotalCost = totalCost,
                ReturnsQuantity = retQty,
                NetSpend = totalCost - retSpend
            });
        }

        var allDates = purchases.Select(p => p.PurchaseDate.Date)
            .Union(purchaseReturns.Select(pr => pr.ReturnDate.Date))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var payments = await _context.SupplierPayments
            .AsNoTracking()
            .Where(sp => !sp.IsCancelled && sp.PaymentDate >= dateRange.StartDate && sp.PaymentDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        foreach (var d in allDates)
        {
            var dayPurchases = purchases.Where(p => p.PurchaseDate.Date == d).ToList();
            var dayReturns = purchaseReturns.Where(pr => pr.ReturnDate.Date == d).ToList();
            var dayPayments = payments.Where(sp => sp.PaymentDate.Date == d).ToList();

            report.ByDayRows.Add(new PurchaseDailyRowDto
            {
                Date = d,
                InvoiceCount = dayPurchases.Count,
                PurchasesTotal = dayPurchases.Sum(x => x.GrandTotal),
                ReturnsTotal = dayReturns.Sum(x => x.TotalAmount),
                PaymentsMade = dayPayments.Sum(x => x.Amount)
            });
        }

        var today = DateTime.Today;
        var allUnpaidPurchases = await _context.Purchases
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Status == PurchaseStatus.Posted && p.GrandTotal > p.PaidAmount)
            .ToListAsync(cancellationToken);

        foreach (var sup in suppliers)
        {
            supplierBalances.TryGetValue(sup.Id, out decimal bal);
            if (bal == 0m) continue;

            var supPurchases = allUnpaidPurchases.Where(p => p.SupplierId == sup.Id).ToList();

            decimal currentB = 0m;
            decimal b31 = 0m;
            decimal b61 = 0m;
            decimal over90 = 0m;

            foreach (var p in supPurchases)
            {
                decimal unpaid = p.GrandTotal - p.PaidAmount;
                int age = (today - p.PurchaseDate.Date).Days;
                if (age <= 30) currentB += unpaid;
                else if (age <= 60) b31 += unpaid;
                else if (age <= 90) b61 += unpaid;
                else over90 += unpaid;
            }

            decimal calculatedTotal = currentB + b31 + b61 + over90;
            if (calculatedTotal != bal)
            {
                decimal diff = bal - calculatedTotal;
                currentB += diff;
            }

            report.AgingRows.Add(new SupplierPayablesAgingRowDto
            {
                SupplierId = sup.Id,
                SupplierName = sup.Name,
                Phone = sup.Phone ?? "",
                TotalBalance = bal,
                CurrentBucket = currentB,
                Bucket31To60 = b31,
                Bucket61To90 = b61,
                BucketOver90 = over90
            });
        }

        report.TotalNetPurchases = report.BySupplierRows.Sum(s => s.NetPurchases);
        report.TotalPaid = report.BySupplierRows.Sum(s => s.PaidAmount);
        report.TotalPayablesBalance = supplierBalances.Values.Sum(b => Math.Max(0m, b));

        return report;
    }

    // =========================================================================
    // 5. TAX REPORT
    // =========================================================================

    public async Task<TaxReportDto> GetTaxReportAsync(
        DateRangeDto dateRange,
        CancellationToken cancellationToken = default)
    {
        var report = new TaxReportDto { DateRange = dateRange };

        var saleItems = await _context.SaleItems
            .AsNoTracking()
            .Where(si => !si.Sale!.IsDeleted && si.Sale.Status == SaleStatus.Completed &&
                         si.Sale.SaleDate >= dateRange.StartDate && si.Sale.SaleDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var returnItems = await _context.SaleReturnItems
            .AsNoTracking()
            .Where(ri => !ri.SaleReturn!.IsDeleted && ri.SaleReturn.Status == SaleReturnStatus.Posted &&
                         ri.SaleReturn.ReturnDate >= dateRange.StartDate && ri.SaleReturn.ReturnDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var saleTaxRates = saleItems.Select(s => s.TaxPercent)
            .Union(returnItems.Select(r => r.TaxPercent))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        foreach (var rate in saleTaxRates)
        {
            var sItems = saleItems.Where(i => i.TaxPercent == rate).ToList();
            var rItems = returnItems.Where(i => i.TaxPercent == rate).ToList();

            decimal grossTax = sItems.Sum(i => Math.Round(i.LineTotal * (rate / 100m), 2));
            decimal retTax = rItems.Sum(i => Math.Round(i.LineTotal * (rate / 100m), 2));
            decimal taxable = sItems.Sum(i => i.LineTotal) - rItems.Sum(i => i.LineTotal);

            report.OutputTaxRows.Add(new TaxRateSummaryDto
            {
                TaxRatePercent = rate,
                TaxableAmount = taxable,
                GrossTax = grossTax,
                ReturnsTax = retTax
            });
        }

        var purchaseItems = await _context.PurchaseItems
            .AsNoTracking()
            .Where(pi => !pi.Purchase!.IsDeleted && pi.Purchase.Status == PurchaseStatus.Posted &&
                         pi.Purchase.PurchaseDate >= dateRange.StartDate && pi.Purchase.PurchaseDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var purchaseReturnItems = await _context.PurchaseReturnItems
            .AsNoTracking()
            .Where(pri => !pri.PurchaseReturn!.IsDeleted && pri.PurchaseReturn.Status == PurchaseReturnStatus.Posted &&
                          pri.PurchaseReturn.ReturnDate >= dateRange.StartDate && pri.PurchaseReturn.ReturnDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var purchaseTaxRates = purchaseItems.Select(p => p.TaxPercent)
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        foreach (var rate in purchaseTaxRates)
        {
            var pItems = purchaseItems.Where(i => i.TaxPercent == rate).ToList();
            var prItems = purchaseReturnItems.Where(i => i.Product != null && i.Product.TaxPercent == rate).ToList();

            decimal grossTax = pItems.Sum(i => Math.Round(i.LineTotal * (rate / 100m), 2));
            decimal retTax = prItems.Sum(i => Math.Round(i.LineTotal * (rate / 100m), 2));
            decimal taxable = pItems.Sum(i => i.LineTotal) - prItems.Sum(i => i.LineTotal);

            report.InputTaxRows.Add(new TaxRateSummaryDto
            {
                TaxRatePercent = rate,
                TaxableAmount = taxable,
                GrossTax = grossTax,
                ReturnsTax = retTax
            });
        }

        report.TotalTaxableSales = report.OutputTaxRows.Sum(r => r.TaxableAmount);
        report.TotalOutputTax = report.OutputTaxRows.Sum(r => r.NetTax);
        report.TotalTaxablePurchases = report.InputTaxRows.Sum(r => r.TaxableAmount);
        report.TotalInputTax = report.InputTaxRows.Sum(r => r.NetTax);

        return report;
    }

    // =========================================================================
    // 6. STOCK MOVEMENT SUMMARY
    // =========================================================================

    public async Task<StockReportDto> GetStockMovementSummaryAsync(
        DateRangeDto dateRange,
        int slowMovingDays = 30,
        int fastMovingTopN = 10,
        CancellationToken cancellationToken = default)
    {
        var report = new StockReportDto { DateRange = dateRange };

        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var allMovements = await _context.StockMovements
            .AsNoTracking()
            .Where(m => m.MovementDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var today = DateTime.Today;

        foreach (var prod in products)
        {
            var prodMovements = allMovements.Where(m => m.ProductId == prod.Id).ToList();

            decimal opening = prodMovements.Where(m => m.MovementDate < dateRange.StartDate).Sum(m => m.Quantity);

            var periodMovements = prodMovements.Where(m => m.MovementDate >= dateRange.StartDate && m.MovementDate <= dateRange.EndDate).ToList();

            decimal purchasesIn = periodMovements.Where(m => m.MovementType == StockMovementType.Purchase).Sum(m => m.Quantity);
            decimal salesOut = Math.Abs(periodMovements.Where(m => m.MovementType == StockMovementType.Sale).Sum(m => m.Quantity));
            decimal returnsIn = periodMovements.Where(m => m.MovementType == StockMovementType.SaleReturn).Sum(m => m.Quantity);
            decimal adjNet = periodMovements.Where(m => m.MovementType == StockMovementType.Adjustment || m.MovementType == StockMovementType.Opening).Sum(m => m.Quantity);

            decimal closing = opening + purchasesIn - salesOut + returnsIn + adjNet;

            report.MovementSummaryRows.Add(new StockMovementSummaryRowDto
            {
                ProductId = prod.Id,
                Sku = prod.Sku,
                ProductName = prod.Name,
                CategoryName = prod.Category?.Name ?? "General",
                OpeningStock = opening,
                PurchasesIn = purchasesIn,
                SalesOut = salesOut,
                ReturnsIn = returnsIn,
                AdjustmentsNet = adjNet,
                ClosingStock = closing
            });

            decimal currentLedgerStock = prodMovements.Sum(m => m.Quantity);
            if (currentLedgerStock > 0)
            {
                var lastSaleMovement = prodMovements
                    .Where(m => m.MovementType == StockMovementType.Sale)
                    .OrderByDescending(m => m.MovementDate)
                    .FirstOrDefault();

                DateTime? lastSale = lastSaleMovement?.MovementDate;
                int daysSinceSale = lastSale.HasValue ? (today - lastSale.Value.Date).Days : int.MaxValue;

                if (daysSinceSale >= slowMovingDays)
                {
                    report.SlowMovingRows.Add(new SlowMovingStockRowDto
                    {
                        ProductId = prod.Id,
                        Sku = prod.Sku,
                        ProductName = prod.Name,
                        CategoryName = prod.Category?.Name ?? "General",
                        CurrentStock = currentLedgerStock,
                        UnitCost = prod.CostPrice,
                        LastSaleDate = lastSale,
                        DaysSinceLastSale = daysSinceSale == int.MaxValue ? 999 : daysSinceSale
                    });
                }
            }
        }

        report.SlowMovingRows = report.SlowMovingRows.OrderByDescending(r => r.ValueTiedUp).ToList();

        return report;
    }

    // =========================================================================
    // 7. EXPENSES REPORT
    // =========================================================================

    public async Task<ExpensesReportDto> GetExpensesReportAsync(
        DateRangeDto dateRange,
        CancellationToken cancellationToken = default)
    {
        var report = new ExpensesReportDto { DateRange = dateRange };

        var expenses = await _context.Expenses
            .AsNoTracking()
            .Include(e => e.ExpenseCategory)
            .Where(e => !e.IsCancelled && e.ExpenseDate >= dateRange.StartDate && e.ExpenseDate <= dateRange.EndDate)
            .OrderBy(e => e.ExpenseDate)
            .ToListAsync(cancellationToken);

        decimal totalAmount = expenses.Sum(e => e.Amount);
        report.TotalExpenseCount = expenses.Count;
        report.TotalExpenseAmount = totalAmount;

        var catGrouped = expenses.GroupBy(e => new { e.ExpenseCategoryId, Name = e.ExpenseCategory != null ? e.ExpenseCategory.Name : "Other" }).ToList();
        foreach (var g in catGrouped)
        {
            decimal sum = g.Sum(x => x.Amount);
            decimal pct = totalAmount > 0 ? Math.Round((sum / totalAmount) * 100m, 2) : 0m;

            report.ByCategoryRows.Add(new ExpenseCategorySummaryDto
            {
                CategoryId = g.Key.ExpenseCategoryId,
                CategoryName = g.Key.Name,
                ExpenseCount = g.Count(),
                TotalAmount = sum,
                PercentOfTotal = pct
            });
        }
        report.ByCategoryRows = report.ByCategoryRows.OrderByDescending(c => c.TotalAmount).ToList();

        var dayGrouped = expenses.GroupBy(e => e.ExpenseDate.Date).OrderBy(g => g.Key).ToList();
        foreach (var g in dayGrouped)
        {
            report.ByDayRows.Add(new ExpenseDailySummaryDto
            {
                Date = g.Key,
                ExpenseCount = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
                CashAmount = g.Where(x => x.Method == PaymentMethod.Cash).Sum(x => x.Amount),
                CardAmount = g.Where(x => x.Method == PaymentMethod.Card).Sum(x => x.Amount),
                BankAmount = g.Where(x => x.Method == PaymentMethod.Bank).Sum(x => x.Amount),
                MobileWalletAmount = g.Where(x => x.Method == PaymentMethod.MobileWallet).Sum(x => x.Amount),
                OtherAmount = g.Where(x => x.Method == PaymentMethod.Other || x.Method == PaymentMethod.Cheque).Sum(x => x.Amount)
            });
        }

        return report;
    }

    // =========================================================================
    // 8. PAYMENT METHODS REPORT
    // =========================================================================

    public async Task<PaymentMethodsReportDto> GetPaymentMethodsReportAsync(
        DateRangeDto dateRange,
        CancellationToken cancellationToken = default)
    {
        var report = new PaymentMethodsReportDto { DateRange = dateRange };

        var salePayments = await _context.SalePayments
            .AsNoTracking()
            .Where(p => !p.Sale!.IsDeleted && p.Sale.Status == SaleStatus.Completed &&
                        p.PaidAt >= dateRange.StartDate && p.PaidAt <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var saleRefunds = await _context.SaleReturns
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.Status == SaleReturnStatus.Posted &&
                        r.ReturnDate >= dateRange.StartDate && r.ReturnDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var supplierPayments = await _context.SupplierPayments
            .AsNoTracking()
            .Where(sp => !sp.IsCancelled && sp.PaymentDate >= dateRange.StartDate && sp.PaymentDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        var expenses = await _context.Expenses
            .AsNoTracking()
            .Where(e => !e.IsCancelled && e.ExpenseDate >= dateRange.StartDate && e.ExpenseDate <= dateRange.EndDate)
            .ToListAsync(cancellationToken);

        foreach (PaymentMethod method in Enum.GetValues(typeof(PaymentMethod)))
        {
            decimal inAmount = salePayments.Where(p => p.Method == method).Sum(p => p.Amount);
            decimal refunds = saleRefunds.Where(r => r.RefundMethod == method).Sum(r => r.RefundedAmount);
            decimal supPayments = supplierPayments.Where(sp => sp.Method == method).Sum(sp => sp.Amount);
            decimal exp = expenses.Where(e => e.Method == method).Sum(e => e.Amount);

            report.Rows.Add(new PaymentMethodSummaryDto
            {
                Method = method,
                SalesCollected = inAmount,
                RefundsPaid = refunds,
                SupplierPaymentsPaid = supPayments,
                ExpensesPaid = exp
            });
        }

        report.GrandTotalIn = report.Rows.Sum(r => r.TotalMoneyIn);
        report.GrandTotalOut = report.Rows.Sum(r => r.TotalMoneyOut);

        return report;
    }

    // =========================================================================
    // 9. AUDIT LOG VIEWER
    // =========================================================================

    public async Task<AuditLogReportDto> GetAuditLogsAsync(
        AuditLogFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.Timestamp >= filter.DateRange.StartDate && a.Timestamp <= filter.DateRange.EndDate);

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            query = query.Where(a => a.EntityName == filter.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(a => a.Action.Contains(filter.Action));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(filter.PageIndex * filter.PageSize)
            .Take(filter.PageSize)
            .Select(a => new AuditLogRowDto
            {
                Id = a.Id,
                Timestamp = a.Timestamp,
                Action = a.Action,
                EntityName = a.EntityName ?? "System",
                EntityId = a.EntityId,
                Details = a.Details,
                Username = a.User != null ? a.User.Username : "System"
            })
            .ToListAsync(cancellationToken);

        return new AuditLogReportDto
        {
            Rows = logs,
            TotalCount = totalCount,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize
        };
    }

    // =========================================================================
    // 10. BARCODE AND DATA QUALITY REPORT
    // =========================================================================

    public async Task<BarcodeDataQualityReportDto> GetBarcodeDataQualityAsync(
        CancellationToken cancellationToken = default)
    {
        var report = new BarcodeDataQualityReportDto();

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var productBarcodes = await _context.ProductBarcodes
            .AsNoTracking()
            .Where(pb => !pb.IsDeleted)
            .ToListAsync(cancellationToken);

        var stockSums = await _context.StockMovements
            .AsNoTracking()
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Quantity) })
            .ToListAsync(cancellationToken);

        var stockDict = stockSums.ToDictionary(s => s.ProductId, s => s.Stock);

        var allBarcodes = new List<(string Barcode, int ProductId)>();
        foreach (var p in products.Where(p => !string.IsNullOrWhiteSpace(p.Barcode)))
        {
            allBarcodes.Add((p.Barcode!.Trim(), p.Id));
        }
        foreach (var pb in productBarcodes.Where(pb => !string.IsNullOrWhiteSpace(pb.Barcode)))
        {
            allBarcodes.Add((pb.Barcode!.Trim(), pb.ProductId));
        }

        var dupBarcodes = allBarcodes
            .GroupBy(b => b.Barcode)
            .Where(g => g.Select(x => x.ProductId).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        foreach (var p in products)
        {
            stockDict.TryGetValue(p.Id, out decimal stock);

            // 1. Missing barcode
            if (string.IsNullOrWhiteSpace(p.Barcode))
            {
                report.Issues.Add(new DataQualityIssueDto
                {
                    ProductId = p.Id,
                    Sku = p.Sku,
                    ProductName = p.Name,
                    Barcode = null,
                    SalePrice = p.SalePrice,
                    CostPrice = p.CostPrice,
                    CurrentStock = stock,
                    IssueType = DataQualityIssueType.MissingBarcode,
                    Description = "Product has no barcode assigned."
                });
                report.MissingBarcodeCount++;
            }
            else
            {
                string b = p.Barcode.Trim();

                // 2. Duplicate barcode
                if (dupBarcodes.Contains(b))
                {
                    report.Issues.Add(new DataQualityIssueDto
                    {
                        ProductId = p.Id,
                        Sku = p.Sku,
                        ProductName = p.Name,
                        Barcode = b,
                        SalePrice = p.SalePrice,
                        CostPrice = p.CostPrice,
                        CurrentStock = stock,
                        IssueType = DataQualityIssueType.DuplicateBarcode,
                        Description = $"Barcode '{b}' is assigned to multiple products."
                    });
                    report.DuplicateBarcodeCount++;
                }

                // 3. Invalid check digit (EAN-13, UPC-A)
                if (b.Length == 13 && b.All(char.IsDigit))
                {
                    bool valid = _barcodeService.ValidateEan13(b);
                    if (!valid)
                    {
                        report.Issues.Add(new DataQualityIssueDto
                        {
                            ProductId = p.Id,
                            Sku = p.Sku,
                            ProductName = p.Name,
                            Barcode = b,
                            SalePrice = p.SalePrice,
                            CostPrice = p.CostPrice,
                            CurrentStock = stock,
                            IssueType = DataQualityIssueType.InvalidCheckDigit,
                            Description = $"EAN-13 barcode '{b}' has an invalid check digit."
                        });
                        report.InvalidCheckDigitCount++;
                    }
                }
                else if (b.Length == 12 && b.All(char.IsDigit))
                {
                    bool valid = _barcodeService.ValidateUpcA(b);
                    if (!valid)
                    {
                        report.Issues.Add(new DataQualityIssueDto
                        {
                            ProductId = p.Id,
                            Sku = p.Sku,
                            ProductName = p.Name,
                            Barcode = b,
                            SalePrice = p.SalePrice,
                            CostPrice = p.CostPrice,
                            CurrentStock = stock,
                            IssueType = DataQualityIssueType.InvalidCheckDigit,
                            Description = $"UPC-A barcode '{b}' has an invalid check digit."
                        });
                        report.InvalidCheckDigitCount++;
                    }
                }
            }

            // 4. Zero cost price
            if (p.CostPrice <= 0m)
            {
                report.Issues.Add(new DataQualityIssueDto
                {
                    ProductId = p.Id,
                    Sku = p.Sku,
                    ProductName = p.Name,
                    Barcode = p.Barcode,
                    SalePrice = p.SalePrice,
                    CostPrice = p.CostPrice,
                    CurrentStock = stock,
                    IssueType = DataQualityIssueType.ZeroCostPrice,
                    Description = "Product has zero or unassigned cost price."
                });
                report.ZeroCostPriceCount++;
            }

            // 5. Sale price below cost
            if (p.SalePrice < p.CostPrice && p.CostPrice > 0m)
            {
                report.Issues.Add(new DataQualityIssueDto
                {
                    ProductId = p.Id,
                    Sku = p.Sku,
                    ProductName = p.Name,
                    Barcode = p.Barcode,
                    SalePrice = p.SalePrice,
                    CostPrice = p.CostPrice,
                    CurrentStock = stock,
                    IssueType = DataQualityIssueType.PriceBelowCost,
                    Description = $"Selling price (Rs {p.SalePrice:F2}) is below cost price (Rs {p.CostPrice:F2})."
                });
                report.PriceBelowCostCount++;
            }

            // 6. Negative stock
            if (stock < 0m)
            {
                report.Issues.Add(new DataQualityIssueDto
                {
                    ProductId = p.Id,
                    Sku = p.Sku,
                    ProductName = p.Name,
                    Barcode = p.Barcode,
                    SalePrice = p.SalePrice,
                    CostPrice = p.CostPrice,
                    CurrentStock = stock,
                    IssueType = DataQualityIssueType.NegativeStock,
                    Description = $"Inventory ledger has negative stock balance ({stock:0.##})."
                });
                report.NegativeStockCount++;
            }
        }

        return report;
    }

    // =========================================================================
    // 11. DASHBOARD SUMMARY QUERIES
    // =========================================================================

    public async Task<DashboardReportSummaryDto> GetDashboardSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var summary = new DashboardReportSummaryDto();
        var today = DateTime.Today;
        var todayRange = DateRangeDto.FromPreset(ReportDatePreset.Today, today);
        var yesterdayRange = DateRangeDto.FromPreset(ReportDatePreset.Yesterday, today);

        // 1. Today's daily sales
        var todayDaily = await GetDailySalesSummaryAsync(todayRange, cancellationToken);
        summary.TodayNetSales = todayDaily.TotalNetSales;
        summary.TodaySalesCount = todayDaily.TotalSalesCount;

        // Today's profit
        var todayPnl = await GetProfitAndLossAsync(todayRange, cancellationToken);
        summary.TodayGrossProfit = todayPnl.CurrentGrossProfit;

        // 2. Yesterday's comparison
        var yesterdayDaily = await GetDailySalesSummaryAsync(yesterdayRange, cancellationToken);
        var yesterdayPnl = await GetProfitAndLossAsync(yesterdayRange, cancellationToken);

        if (yesterdayDaily.TotalNetSales > 0)
        {
            summary.SalesVsYesterdayPercent = Math.Round(((summary.TodayNetSales - yesterdayDaily.TotalNetSales) / yesterdayDaily.TotalNetSales) * 100m, 1);
        }

        if (yesterdayPnl.CurrentGrossProfit > 0)
        {
            summary.ProfitVsYesterdayPercent = Math.Round(((summary.TodayGrossProfit - yesterdayPnl.CurrentGrossProfit) / yesterdayPnl.CurrentGrossProfit) * 100m, 1);
        }

        if (yesterdayDaily.TotalSalesCount > 0)
        {
            summary.SalesCountVsYesterdayDiff = summary.TodaySalesCount - yesterdayDaily.TotalSalesCount;
        }

        // 3. Held bills count
        summary.HeldBillsCount = await _context.Sales
            .AsNoTracking()
            .CountAsync(s => !s.IsDeleted && s.Status == SaleStatus.Held && s.HoldNumber != null, cancellationToken);

        // 4. Supplier payables using existing ledger balance query
        summary.SupplierPayables = await _supplierLedgerRepo.GetTotalPayableBalanceAsync(cancellationToken);

        // 5. Last 30 days daily net sales trend
        var thirtyDaysRange = DateRangeDto.FromPreset(ReportDatePreset.Custom, today.AddDays(-29), today.AddDays(-29), today);
        var thirtyDaysDaily = await GetDailySalesSummaryAsync(thirtyDaysRange, cancellationToken);
        var dailyDict = thirtyDaysDaily.Rows.ToDictionary(r => r.Date.Date, r => r.NetSales);

        for (int i = 29; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            dailyDict.TryGetValue(d, out decimal net);
            summary.Last30DaysTrend.Add(new DailySalesTrendPointDto
            {
                Date = d,
                NetSales = net
            });
        }

        // 6. Top 5 products sold today
        var topProductsReport = await GetSalesByProductAsync(todayRange, SalesGroupBy.Product, topN: 5, sortBy: "revenue", sortDescending: true, cancellationToken);
        summary.Top5ProductsToday = topProductsReport.Rows.Select(r => new TopSellingProductDto
        {
            ProductId = r.ProductId ?? 0,
            ProductName = r.Name,
            Sku = r.Sku,
            Quantity = r.QuantitySold,
            Revenue = r.NetRevenue
        }).ToList();

        // 7. Stock status counts
        var products = await _context.Products.AsNoTracking().Where(p => !p.IsDeleted).ToListAsync(cancellationToken);
        summary.TotalProductsCount = products.Count;

        var stockSums = await _context.StockMovements
            .AsNoTracking()
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Quantity) })
            .ToListAsync(cancellationToken);

        var stockDict = stockSums.ToDictionary(s => s.ProductId, s => s.Stock);

        int low = 0;
        int outStock = 0;
        foreach (var p in products)
        {
            stockDict.TryGetValue(p.Id, out decimal s);
            if (s <= 0) outStock++;
            else if (s <= p.ReorderLevel) low++;
        }
        summary.LowStockCount = low;
        summary.OutOfStockCount = outStock;

        // 8. Checklist booleans
        summary.HasCompletedSaleToday = summary.TodaySalesCount > 0;
        summary.LowStockReviewed = low == 0;
        summary.DatabaseBackupPresent = CheckIfBackupExists();

        return summary;
    }

    private static bool CheckIfBackupExists()
    {
        try
        {
            string backupDir = @"C:\ShopData\Backups";
            if (Directory.Exists(backupDir))
            {
                return Directory.GetFiles(backupDir, "*.db").Length > 0;
            }
        }
        catch { }
        return false;
    }

    // =========================================================================
    // 12. EXPORT HELPERS: CSV & PDF
    // =========================================================================

    public byte[] ExportReportToCsv(string reportType, object reportData)
    {
        var sb = new StringBuilder();
        var preamble = Encoding.UTF8.GetPreamble();

        switch (reportData)
        {
            case DailySalesSummaryReportDto daily:
                sb.AppendLine("Date,Sales Count,Gross Sales,Discounts,Tax,Returns,Net Sales,Cash,Card,Bank,Mobile Wallet,Other");
                foreach (var r in daily.Rows)
                {
                    sb.AppendLine($"{r.Date:yyyy-MM-dd},{r.SalesCount},{r.GrossSales:F2},{r.Discounts:F2},{r.Tax:F2},{r.Returns:F2},{r.NetSales:F2},{r.CashAmount:F2},{r.CardAmount:F2},{r.BankAmount:F2},{r.MobileWalletAmount:F2},{r.OtherAmount:F2}");
                }
                sb.AppendLine($"TOTAL,{daily.TotalSalesCount},{daily.TotalGrossSales:F2},{daily.TotalDiscounts:F2},{daily.TotalTax:F2},{daily.TotalReturns:F2},{daily.TotalNetSales:F2},{daily.TotalCash:F2},{daily.TotalCard:F2},{daily.TotalBank:F2},{daily.TotalMobileWallet:F2},{daily.TotalOther:F2}");
                break;

            case SalesByProductReportDto prod:
                sb.AppendLine("SKU,Name,Category,Brand,Quantity Sold,Gross Revenue,Discounts,Returns,Net Revenue,Cost,Profit,Margin %");
                foreach (var r in prod.Rows)
                {
                    sb.AppendLine($"\"{EscapeCsv(r.Sku)}\",\"{EscapeCsv(r.Name)}\",\"{EscapeCsv(r.CategoryName)}\",\"{EscapeCsv(r.BrandName)}\",{r.QuantitySold:0.##},{r.GrossRevenue:F2},{r.DiscountAmount:F2},{r.ReturnAmount:F2},{r.NetRevenue:F2},{r.Cost:F2},{r.Profit:F2},{r.MarginPercent:F2}");
                }
                sb.AppendLine($"TOTAL,,, ,{prod.TotalQuantitySold:0.##},,,,,{prod.TotalNetRevenue:F2},{prod.TotalCost:F2},{prod.TotalProfit:F2},{prod.OverallMarginPercent:F2}");
                break;

            case ProfitAndLossReportDto pnl:
                sb.AppendLine("Metric,Current Period,Previous Period,Change");
                sb.AppendLine($"Gross Sales,{pnl.CurrentGrossSales:F2},{pnl.PreviousGrossSales:F2},{pnl.CurrentGrossSales - pnl.PreviousGrossSales:F2}");
                sb.AppendLine($"Sales Discounts,-{pnl.CurrentSalesDiscounts:F2},-{pnl.PreviousSalesDiscounts:F2},-{pnl.CurrentSalesDiscounts - pnl.PreviousSalesDiscounts:F2}");
                sb.AppendLine($"Sales Returns,-{pnl.CurrentSalesReturns:F2},-{pnl.PreviousSalesReturns:F2},-{pnl.CurrentSalesReturns - pnl.PreviousSalesReturns:F2}");
                sb.AppendLine($"NET SALES,{pnl.CurrentNetSales:F2},{pnl.PreviousNetSales:F2},{pnl.CurrentNetSales - pnl.PreviousNetSales:F2}");
                sb.AppendLine($"Cost of Goods Sold (COGS),-{pnl.CurrentCogs:F2},-{pnl.PreviousCogs:F2},-{pnl.CurrentCogs - pnl.PreviousCogs:F2}");
                sb.AppendLine($"GROSS PROFIT,{pnl.CurrentGrossProfit:F2},{pnl.PreviousGrossProfit:F2},{pnl.CurrentGrossProfit - pnl.PreviousGrossProfit:F2}");
                sb.AppendLine($"Gross Margin %,{pnl.CurrentGrossMarginPercent:F2}%,{pnl.PreviousGrossMarginPercent:F2}%,");
                sb.AppendLine("--- Operating Expenses ---,,,");
                foreach (var exp in pnl.ExpensesByCategory)
                {
                    sb.AppendLine($"  {exp.CategoryName},-{exp.CurrentAmount:F2},-{exp.PreviousAmount:F2},-{exp.ChangeAmount:F2}");
                }
                sb.AppendLine($"Total Operating Expenses,-{pnl.CurrentTotalExpenses:F2},-{pnl.PreviousTotalExpenses:F2},-{pnl.CurrentTotalExpenses - pnl.PreviousTotalExpenses:F2}");
                sb.AppendLine($"Stock Write-Offs (Damaged/Lost/Expired),-{pnl.CurrentStockWriteOffs:F2},-{pnl.PreviousStockWriteOffs:F2},-{pnl.CurrentStockWriteOffs - pnl.PreviousStockWriteOffs:F2}");
                sb.AppendLine($"NET PROFIT,{pnl.CurrentNetProfit:F2},{pnl.PreviousNetProfit:F2},{pnl.CurrentNetProfit - pnl.PreviousNetProfit:F2}");
                sb.AppendLine($"Net Margin %,{pnl.CurrentNetMarginPercent:F2}%,{pnl.PreviousNetMarginPercent:F2}%,");
                break;

            case PurchasesReportDto pur:
                sb.AppendLine("Supplier,Invoices,Subtotal,Discounts,Tax,Freight,Grand Total,Returns,Net Purchases,Paid,Current Balance");
                foreach (var r in pur.BySupplierRows)
                {
                    sb.AppendLine($"\"{EscapeCsv(r.SupplierName)}\",{r.InvoiceCount},{r.SubTotal:F2},{r.DiscountAmount:F2},{r.TaxAmount:F2},{r.FreightAmount:F2},{r.GrandTotal:F2},{r.ReturnsTotal:F2},{r.NetPurchases:F2},{r.PaidAmount:F2},{r.CurrentBalance:F2}");
                }
                break;

            case TaxReportDto tax:
                sb.AppendLine("Tax Rate %,Taxable Amount,Gross Tax,Returns Tax,Net Tax");
                sb.AppendLine("--- OUTPUT TAX (SALES) ---");
                foreach (var r in tax.OutputTaxRows)
                {
                    sb.AppendLine($"{r.TaxRatePercent:F2}%,{r.TaxableAmount:F2},{r.GrossTax:F2},{r.ReturnsTax:F2},{r.NetTax:F2}");
                }
                sb.AppendLine("--- INPUT TAX (PURCHASES) ---");
                foreach (var r in tax.InputTaxRows)
                {
                    sb.AppendLine($"{r.TaxRatePercent:F2}%,{r.TaxableAmount:F2},{r.GrossTax:F2},{r.ReturnsTax:F2},{r.NetTax:F2}");
                }
                sb.AppendLine($"NET TAX PAYABLE,,,,{tax.NetTaxPayable:F2}");
                break;

            case StockReportDto stock:
                sb.AppendLine("SKU,Name,Category,Opening,Purchases In,Sales Out,Returns In,Adjustments,Closing");
                foreach (var r in stock.MovementSummaryRows)
                {
                    sb.AppendLine($"\"{EscapeCsv(r.Sku)}\",\"{EscapeCsv(r.ProductName)}\",\"{EscapeCsv(r.CategoryName)}\",{r.OpeningStock:0.##},{r.PurchasesIn:0.##},{r.SalesOut:0.##},{r.ReturnsIn:0.##},{r.AdjustmentsNet:0.##},{r.ClosingStock:0.##}");
                }
                break;

            case ExpensesReportDto exp:
                sb.AppendLine("Category,Count,Total Amount,Percent of Total");
                foreach (var r in exp.ByCategoryRows)
                {
                    sb.AppendLine($"\"{EscapeCsv(r.CategoryName)}\",{r.ExpenseCount},{r.TotalAmount:F2},{r.PercentOfTotal:F2}%");
                }
                sb.AppendLine($"TOTAL,{exp.TotalExpenseCount},{exp.TotalExpenseAmount:F2},100.00%");
                break;

            case PaymentMethodsReportDto pm:
                sb.AppendLine("Payment Method,Sales Collected (In),Refunds Paid (Out),Supplier Payments (Out),Expenses (Out),Net Cash Flow");
                foreach (var r in pm.Rows)
                {
                    sb.AppendLine($"{r.MethodName},{r.SalesCollected:F2},{r.RefundsPaid:F2},{r.SupplierPaymentsPaid:F2},{r.ExpensesPaid:F2},{r.NetCashFlow:F2}");
                }
                sb.AppendLine($"TOTAL,{pm.GrandTotalIn:F2},,,,{pm.NetTotalFlow:F2}");
                break;

            case AuditLogReportDto audit:
                sb.AppendLine("Timestamp,Action,Entity,Record ID,Username,Details");
                foreach (var r in audit.Rows)
                {
                    sb.AppendLine($"{r.Timestamp:yyyy-MM-dd HH:mm:ss},\"{EscapeCsv(r.Action)}\",\"{EscapeCsv(r.EntityName)}\",{r.EntityId},\"{EscapeCsv(r.Username ?? "")}\",\"{EscapeCsv(r.Details ?? "")}\"");
                }
                break;

            case BarcodeDataQualityReportDto dq:
                sb.AppendLine("SKU,Product Name,Barcode,Issue Type,Sale Price,Cost Price,Current Stock,Description");
                foreach (var r in dq.Issues)
                {
                    sb.AppendLine($"\"{EscapeCsv(r.Sku)}\",\"{EscapeCsv(r.ProductName)}\",\"{EscapeCsv(r.Barcode ?? "")}\",{r.IssueType},{r.SalePrice:F2},{r.CostPrice:F2},{r.CurrentStock:0.##},\"{EscapeCsv(r.Description)}\"");
                }
                break;

            default:
                sb.AppendLine("Report,Generated");
                sb.AppendLine($"{reportType},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                break;
        }

        var contentBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + contentBytes.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(contentBytes, 0, result, preamble.Length, contentBytes.Length);
        return result;
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }

    public byte[] ExportReportToPdf(string reportType, object reportData, ShopSettingDto settings)
    {
        var document = CreateReportQuestDocument(reportType, reportData, settings);
        return document.GeneratePdf();
    }

    public IReadOnlyList<byte[]> RenderReportPageImages(string reportType, object reportData, ShopSettingDto settings)
    {
        var document = CreateReportQuestDocument(reportType, reportData, settings);
        return document.GenerateImages().ToList();
    }

    private IDocument CreateReportQuestDocument(string reportType, object reportData, ShopSettingDto settings)
    {
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, QuestPDF.Infrastructure.Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#1E293B"));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(sc =>
                        {
                            sc.Item().Text(settings.ShopName).FontSize(16).Bold().FontColor("#0F172A");
                            if (!string.IsNullOrWhiteSpace(settings.Address))
                            {
                                sc.Item().Text(settings.Address).FontSize(8).FontColor("#64748B");
                            }
                            if (!string.IsNullOrWhiteSpace(settings.Phone))
                            {
                                sc.Item().Text($"Phone: {settings.Phone}").FontSize(8).FontColor("#64748B");
                            }
                        });

                        row.RelativeItem().AlignRight().Column(rc =>
                        {
                            rc.Item().Text(reportType.ToUpperInvariant()).FontSize(14).Bold().FontColor("#2563EB");
                            rc.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8).FontColor("#64748B");
                        });
                    });

                    col.Item().PaddingVertical(6).LineHorizontal(1).LineColor("#CBD5E1");
                });

                // Content
                page.Content().PaddingVertical(8).Element(content =>
                {
                    RenderPdfContent(content, reportType, reportData, settings);
                });

                // Footer with Page Numbers
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Generated by ShopPOS - Offline Desktop").FontSize(8).FontColor("#94A3B8");
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });
        });
    }

    private void RenderPdfContent(IContainer container, string reportType, object reportData, ShopSettingDto settings)
    {
        container.Column(col =>
        {
            switch (reportData)
            {
                case DailySalesSummaryReportDto daily:
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Date").Bold();
                            h.Cell().AlignRight().Text("Sales").Bold();
                            h.Cell().AlignRight().Text("Gross (Rs)").Bold();
                            h.Cell().AlignRight().Text("Disc (Rs)").Bold();
                            h.Cell().AlignRight().Text("Tax (Rs)").Bold();
                            h.Cell().AlignRight().Text("Returns (Rs)").Bold();
                            h.Cell().AlignRight().Text("Net (Rs)").Bold();
                        });

                        foreach (var r in daily.Rows)
                        {
                            table.Cell().Text(r.Date.ToString("yyyy-MM-dd"));
                            table.Cell().AlignRight().Text(r.SalesCount.ToString());
                            table.Cell().AlignRight().Text(r.GrossSales.ToString("N2"));
                            table.Cell().AlignRight().Text(r.Discounts.ToString("N2"));
                            table.Cell().AlignRight().Text(r.Tax.ToString("N2"));
                            table.Cell().AlignRight().Text(r.Returns.ToString("N2"));
                            table.Cell().AlignRight().Text(r.NetSales.ToString("N2")).Bold();
                        }

                        table.Cell().Text("TOTAL").Bold();
                        table.Cell().AlignRight().Text(daily.TotalSalesCount.ToString()).Bold();
                        table.Cell().AlignRight().Text(daily.TotalGrossSales.ToString("N2")).Bold();
                        table.Cell().AlignRight().Text(daily.TotalDiscounts.ToString("N2")).Bold();
                        table.Cell().AlignRight().Text(daily.TotalTax.ToString("N2")).Bold();
                        table.Cell().AlignRight().Text(daily.TotalReturns.ToString("N2")).Bold();
                        table.Cell().AlignRight().Text(daily.TotalNetSales.ToString("N2")).Bold();
                    });
                    break;

                case ProfitAndLossReportDto pnl:
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(5);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(3);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Statement Line").Bold();
                            h.Cell().AlignRight().Text("Current (Rs)").Bold();
                            h.Cell().AlignRight().Text("Previous (Rs)").Bold();
                            h.Cell().AlignRight().Text("Change (Rs)").Bold();
                        });

                        AddPnlRow(table, "Gross Sales", pnl.CurrentGrossSales, pnl.PreviousGrossSales);
                        AddPnlRow(table, "Less: Discounts", -pnl.CurrentSalesDiscounts, -pnl.PreviousSalesDiscounts);
                        AddPnlRow(table, "Less: Returns", -pnl.CurrentSalesReturns, -pnl.PreviousSalesReturns);
                        AddPnlRow(table, "NET SALES", pnl.CurrentNetSales, pnl.PreviousNetSales, isBold: true);
                        AddPnlRow(table, "Cost of Goods Sold (COGS)", -pnl.CurrentCogs, -pnl.PreviousCogs);
                        AddPnlRow(table, "GROSS PROFIT", pnl.CurrentGrossProfit, pnl.PreviousGrossProfit, isBold: true);
                        AddPnlRow(table, "Total Operating Expenses", -pnl.CurrentTotalExpenses, -pnl.PreviousTotalExpenses);
                        AddPnlRow(table, "Stock Write-Offs", -pnl.CurrentStockWriteOffs, -pnl.PreviousStockWriteOffs);
                        AddPnlRow(table, "NET PROFIT", pnl.CurrentNetProfit, pnl.PreviousNetProfit, isBold: true);
                    });
                    break;

                default:
                    col.Item().Text($"Summary of {reportType} generated successfully.").FontSize(10);
                    break;
            }
        });
    }

    private static void AddPnlRow(QuestPDF.Fluent.TableDescriptor table, string label, decimal current, decimal prev, bool isBold = false)
    {
        var c1 = table.Cell().Text(label);
        var c2 = table.Cell().AlignRight().Text(current.ToString("N2"));
        var c3 = table.Cell().AlignRight().Text(prev.ToString("N2"));
        var c4 = table.Cell().AlignRight().Text((current - prev).ToString("N2"));

        if (isBold)
        {
            c1.Bold();
            c2.Bold();
            c3.Bold();
            c4.Bold();
        }
    }
}
