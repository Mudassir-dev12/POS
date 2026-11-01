using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a snapshot line item within a sale transaction.
/// </summary>
public class SaleItem : BaseEntity
{
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }

    public decimal Quantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;
    public decimal CostPriceSnapshot { get; set; } = 0m;
    public decimal DiscountPercent { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal TaxPercent { get; set; } = 0m;
    public decimal LineTotal { get; set; } = 0m;

    public string? Note { get; set; }
    public int LineNo { get; set; }
}

