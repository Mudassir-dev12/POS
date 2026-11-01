using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a product category supporting hierarchical parent-child relationships.
/// </summary>
public class Category : BaseEntity
{
    /// <summary>
    /// Gets or sets the name of the category (unique per parent).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional parent category ID.
    /// </summary>
    public int? ParentCategoryId { get; set; }

    /// <summary>
    /// Navigation property to parent category.
    /// </summary>
    public Category? ParentCategory { get; set; }

    /// <summary>
    /// Gets or sets whether this category is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation collection of subcategories.
    /// </summary>
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();

    /// <summary>
    /// Navigation collection of products under this category.
    /// </summary>
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
