using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Reports;

public class DailySalesTrendPointDto
{
    public DateTime Date { get; set; }
    public decimal NetSales { get; set; }
    public string FormattedDate => Date.ToString("dd MMM");
}

public class TopSellingProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Revenue { get; set; }
}

public class DashboardReportSummaryDto
{
    // KPIs Today
    public decimal TodayNetSales { get; set; }
    public decimal TodayGrossProfit { get; set; }
    public int TodaySalesCount { get; set; }
    public int HeldBillsCount { get; set; }
    public decimal SupplierPayables { get; set; }

    // Comparisons vs Yesterday (only populated if yesterday had data)
    public decimal? SalesVsYesterdayPercent { get; set; }
    public decimal? ProfitVsYesterdayPercent { get; set; }
    public int? SalesCountVsYesterdayDiff { get; set; }

    // 30-Day Trend
    public List<DailySalesTrendPointDto> Last30DaysTrend { get; set; } = new();

    // Top 5 Products Today
    public List<TopSellingProductDto> Top5ProductsToday { get; set; } = new();

    // Stock & Payables summary
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public int TotalProductsCount { get; set; }

    // Checklist booleans
    public bool HasCompletedSaleToday { get; set; }
    public bool LowStockReviewed { get; set; }
    public bool DatabaseBackupPresent { get; set; }
}

