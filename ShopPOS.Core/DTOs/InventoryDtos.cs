using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs;

public class StockOverviewFilterQuery
{
    public string? SearchTerm { get; set; }
    public int? CategoryId { get; set; }
    public int? BrandId { get; set; }
    public StockStatus? Status { get; set; }
    public bool LowOrOutOnly { get; set; }
    public bool IncludeNonTracked { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class StockOverviewItemDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? BrandId { get; set; }
    public string? BrandName { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public StockStatus Status { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? StockValueAtCost { get; set; }
    public decimal StockValueAtSale { get; set; }
    public bool TrackStock { get; set; }
}

public class StockOverviewSummaryDto
{
    public decimal TotalUnits { get; set; }
    public decimal? TotalValueAtCost { get; set; }
    public decimal TotalValueAtSale { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public int NegativeStockCount { get; set; }
    public int TotalProductCount { get; set; }
}

public class MovementHistoryFilterQuery
{
    public int? ProductId { get; set; }
    public StockMovementType? MovementType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ReferenceSearch { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class MovementHistoryItemDto
{
    public int Id { get; set; }
    public DateTime MovementDate { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public StockMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? UserName { get; set; }
    public string? Note { get; set; }
    public decimal? RunningBalance { get; set; }
}

public class CategoryValuationDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public decimal TotalUnits { get; set; }
    public decimal ValueAtCost { get; set; }
    public decimal ValueAtSale { get; set; }
    public decimal TotalCostValue => ValueAtCost;
    public decimal TotalRetailValue => ValueAtSale;
    public decimal PotentialProfit => ValueAtSale - ValueAtCost;
    public decimal ProfitMarginPercent => ValueAtSale > 0 ? Math.Round((ValueAtSale - ValueAtCost) / ValueAtSale * 100m, 1, MidpointRounding.AwayFromZero) : 0m;
}

public class ReorderItemDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal Multiplier { get; set; }
    public decimal SuggestedQuantity { get; set; }
    public decimal CostPrice { get; set; }
    public StockStatus Status { get; set; }
    public int? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public bool IsSelected { get; set; }
}

public class ReorderPurchaseLineDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class CreatePurchaseFromReorderDto
{
    public int SupplierId { get; set; }
    public List<ReorderPurchaseLineDto> Items { get; set; } = new();
}

public class StockAdjustmentItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal SystemQuantity { get; set; }
    public decimal NewQuantity { get; set; }
    public decimal Difference { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineValueDifference => Math.Round(Difference * UnitCost, 2, MidpointRounding.AwayFromZero);
    public string? Note { get; set; }
}

public class StockAdjustmentListDto : ISelectable
{
    public bool IsSelected { get; set; }
    public int Id { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public DateTime AdjustmentDate { get; set; }
    public StockAdjustmentReason Reason { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalValueDifference { get; set; }
    public StockAdjustmentStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class StockAdjustmentDetailDto
{
    public int Id { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public DateTime AdjustmentDate { get; set; }
    public StockAdjustmentReason Reason { get; set; }
    public string? Notes { get; set; }
    public StockAdjustmentStatus Status { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? PostedByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledByName { get; set; }
    public string? CancelReason { get; set; }
    public decimal TotalValueDifference => Items.Sum(i => i.LineValueDifference);
    public List<StockAdjustmentItemDto> Items { get; set; } = new();
}

public class CreateStockAdjustmentItemDto
{
    public int ProductId { get; set; }
    public decimal NewQuantity { get; set; }
    public string? Note { get; set; }
}

public class CreateStockAdjustmentDto
{
    public DateTime AdjustmentDate { get; set; } = DateTime.Today;
    public StockAdjustmentReason Reason { get; set; } = StockAdjustmentReason.CountCorrection;
    public string? Notes { get; set; }
    public List<CreateStockAdjustmentItemDto> Items { get; set; } = new();
}

public class UpdateStockAdjustmentDto : CreateStockAdjustmentDto
{
    public int Id { get; set; }
}

public class DashboardStatsDto
{
    public int TotalProducts { get; set; }
    public int OutOfStockCount { get; set; }
    public int LowStockCount { get; set; }
    public decimal StockValueAtCost { get; set; }
    public decimal SupplierPayableTotal { get; set; }
    public int PurchasesThisMonthCount { get; set; }
    public decimal PurchasesThisMonthAmount { get; set; }
}
