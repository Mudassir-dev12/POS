using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a product brand or manufacturer.
/// </summary>
public class Brand : BaseEntity
{
    /// <summary>
    /// Gets or sets the name of the brand.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description of the brand.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this brand is active for selection.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation collection of products under this brand.
    /// </summary>
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
