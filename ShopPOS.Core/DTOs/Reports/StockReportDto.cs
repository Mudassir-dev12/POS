using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Reports;

public class StockMovementSummaryRowDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;

    public decimal OpeningStock { get; set; }
    public decimal PurchasesIn { get; set; }
    public decimal SalesOut { get; set; }
    public decimal ReturnsIn { get; set; }
    public decimal AdjustmentsNet { get; set; }
    public decimal ClosingStock { get; set; }
}

public class SlowMovingStockRowDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal UnitCost { get; set; }
    public decimal ValueTiedUp => CurrentStock * UnitCost;
    public DateTime? LastSaleDate { get; set; }
    public int DaysSinceLastSale { get; set; }
}

public class StockReportDto
{
    public DateRangeDto DateRange { get; set; } = new();
    public List<StockMovementSummaryRowDto> MovementSummaryRows { get; set; } = new();
    public List<SlowMovingStockRowDto> SlowMovingRows { get; set; } = new();
}

