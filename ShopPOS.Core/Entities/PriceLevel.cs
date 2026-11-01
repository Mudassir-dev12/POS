using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a pricing tier or customer classification (e.g., Retail, Wholesale, VIP).
/// </summary>
public class PriceLevel : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public ICollection<ProductPrice> ProductPrices { get; set; } = new List<ProductPrice>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}

