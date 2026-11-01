using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

public class StockAdjustmentItem : BaseEntity
{
    public int StockAdjustmentId { get; set; }
    public StockAdjustment? StockAdjustment { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;

    public decimal SystemQuantity { get; set; }
    public decimal NewQuantity { get; set; }
    public decimal Difference { get; set; }
    public decimal UnitCost { get; set; }

    public string? Note { get; set; }
}

