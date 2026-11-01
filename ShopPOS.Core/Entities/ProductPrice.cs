using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Defines an explicit tier price for a product under a specific price level.
/// </summary>
public class ProductPrice : BaseEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int PriceLevelId { get; set; }
    public PriceLevel? PriceLevel { get; set; }

    public decimal Price { get; set; }
}

