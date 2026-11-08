using System;
using System.Collections.Generic;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs.Reports;

public class SalesByProductRowDto
{
    public int? ProductId { get; set; }
    public string GroupKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;

    public decimal QuantitySold { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit => NetRevenue - Cost;
    public decimal MarginPercent => NetRevenue != 0 ? Math.Round((Profit / NetRevenue) * 100m, 2) : 0m;
}

public class SalesByProductReportDto
{
    public DateRangeDto DateRange { get; set; } = new();
    public SalesGroupBy GroupBy { get; set; } = SalesGroupBy.Product;
    public List<SalesByProductRowDto> Rows { get; set; } = new();

    public decimal TotalQuantitySold { get; set; }
    public decimal TotalNetRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit => TotalNetRevenue - TotalCost;
    public decimal OverallMarginPercent => TotalNetRevenue != 0 ? Math.Round((TotalProfit / TotalNetRevenue) * 100m, 2) : 0m;
}

