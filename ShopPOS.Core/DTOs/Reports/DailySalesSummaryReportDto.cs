using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Reports;

public class DailySalesSummaryRowDto
{
    public DateTime Date { get; set; }
    public int SalesCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal Tax { get; set; }
    public decimal Returns { get; set; }
    public decimal NetSales { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CardAmount { get; set; }
    public decimal BankAmount { get; set; }
    public decimal MobileWalletAmount { get; set; }
    public decimal OtherAmount { get; set; }
    public decimal AverageSaleValue => SalesCount > 0 ? Math.Round(NetSales / SalesCount, 2) : 0m;
}

public class DailySalesSummaryReportDto
{
    public DateRangeDto DateRange { get; set; } = new();
    public List<DailySalesSummaryRowDto> Rows { get; set; } = new();

    // Summary Totals
    public int TotalSalesCount { get; set; }
    public decimal TotalGrossSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalReturns { get; set; }
    public decimal TotalNetSales { get; set; }
    public decimal TotalCash { get; set; }
    public decimal TotalCard { get; set; }
    public decimal TotalBank { get; set; }
    public decimal TotalMobileWallet { get; set; }
    public decimal TotalOther { get; set; }
    public decimal OverallAverageSaleValue => TotalSalesCount > 0 ? Math.Round(TotalNetSales / TotalSalesCount, 2) : 0m;
}

