using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents an item line returned by a customer.
/// </summary>
public class SaleReturnItem : BaseEntity
{
    public int SaleReturnId { get; set; }
    public SaleReturn? SaleReturn { get; set; }

    public int? SaleItemId { get; set; }
    public SaleItem? SaleItem { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }

    public decimal Quantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;
    public decimal CostPriceSnapshot { get; set; } = 0m;
    public decimal TaxPercent { get; set; } = 0m;
    public decimal LineTotal { get; set; } = 0m;

    public bool ReturnToStock { get; set; } = true;
}

