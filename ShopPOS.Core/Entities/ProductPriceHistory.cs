using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Records an immutable audit entry whenever a product's cost or sale price is modified.
/// </summary>
public class ProductPriceHistory : BaseEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal OldCostPrice { get; set; }
    public decimal NewCostPrice { get; set; }
    public decimal OldSalePrice { get; set; }
    public decimal NewSalePrice { get; set; }

    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

