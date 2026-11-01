using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a measurement unit for inventory items (e.g., pcs, kg, litre).
/// </summary>
public class Unit : BaseEntity
{
    /// <summary>
    /// Gets or sets the full unit name (e.g. "Kilogram", "Piece").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the abbreviated short unit name (e.g. "kg", "pcs", "box").
    /// </summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this unit supports decimal/fractional quantities (e.g. kg, grams, metres).
    /// </summary>
    public bool AllowDecimal { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this unit is active for selection.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation collection of products using this unit.
    /// </summary>
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
