using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents an individual item line on a purchase invoice.
/// </summary>
public class PurchaseItem : BaseEntity
{
    public int PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>
    /// Historical snapshot of Product Name at the time of purchase.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Historical snapshot of Product SKU at the time of purchase.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal LineTotal { get; set; }

    public string? Note { get; set; }
}

