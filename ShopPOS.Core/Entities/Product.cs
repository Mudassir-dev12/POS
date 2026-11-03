using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents an item in the catalog available for purchase and sale.
/// </summary>
public class Product : BaseEntity
{
    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique Stock Keeping Unit (SKU).
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scannable primary barcode (UPC/EAN), if available.
    /// Kept in sync with the primary ProductBarcode record.
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// Gets or sets the normalized, lowercase search text combining Name, SKU, and all Barcodes for high-speed POS search.
    /// </summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Computes the normalized lowercase search string from product properties and optional barcodes.
    /// </summary>
    public string ComputeSearchText(IEnumerable<string>? extraBarcodes = null)
    {
        var parts = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(Name)) parts.Add(Name.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(Sku)) parts.Add(Sku.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(Barcode)) parts.Add(Barcode.Trim().ToLowerInvariant());
        if (extraBarcodes != null)
        {
            foreach (var b in extraBarcodes)
            {
                if (!string.IsNullOrWhiteSpace(b)) parts.Add(b.Trim().ToLowerInvariant());
            }
        }
        else if (AdditionalBarcodes != null)
        {
            foreach (var ab in AdditionalBarcodes.Where(b => !b.IsDeleted && !string.IsNullOrWhiteSpace(b.Barcode)))
            {
                parts.Add(ab.Barcode.Trim().ToLowerInvariant());
            }
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Gets or sets the optional detailed description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the relative image file path under C:\ShopData\Images.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to Category.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Navigation property to Category.
    /// </summary>
    public Category? Category { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to Brand, if applicable.
    /// </summary>
    public int? BrandId { get; set; }

    /// <summary>
    /// Navigation property to Brand.
    /// </summary>
    public Brand? Brand { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to Unit.
    /// </summary>
    public int UnitId { get; set; }

    /// <summary>
    /// Navigation property to Unit.
    /// </summary>
    public Unit? Unit { get; set; }

    /// <summary>
    /// Gets or sets the product cost/purchase price.
    /// Stored in SQLite as minor units (x100) via value converter.
    /// </summary>
    public decimal CostPrice { get; set; } = 0m;

    /// <summary>
    /// Gets or sets the standard selling price.
    /// Stored in SQLite as minor units (x100) via value converter.
    /// </summary>
    public decimal SalePrice { get; set; } = 0m;

    /// <summary>
    /// Gets or sets the optional minimum allowable selling price (floor price).
    /// Stored in SQLite as minor units (x100) via value converter.
    /// </summary>
    public decimal? MinSalePrice { get; set; }

    /// <summary>
    /// Gets or sets the sales tax percentage rate for this product.
    /// </summary>
    public decimal TaxPercent { get; set; } = 0m;

    /// <summary>
    /// Gets or sets the stock level that triggers a reorder alert.
    /// Stored in SQLite as thousandths (x1000) via value converter.
    /// </summary>
    public decimal ReorderLevel { get; set; } = 0m;

    /// <summary>
    /// Gets or sets whether inventory is tracked for this product.
    /// </summary>
    public bool TrackStock { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this product is sold by weight via scale.
    /// </summary>
    public bool IsWeighable { get; set; } = false;

    /// <summary>
    /// Gets or sets whether fractional/decimal quantities are allowed when selling.
    /// </summary>
    public bool AllowDecimalQuantity { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this product is actively available for trading.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation collection of stock movements for this product.
    /// </summary>
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    /// <summary>
    /// Navigation collection of alternate / secondary barcodes.
    /// </summary>
    public ICollection<ProductBarcode> AdditionalBarcodes { get; set; } = new List<ProductBarcode>();

    /// <summary>
    /// Navigation collection of historical price change audits.
    /// </summary>
    public ICollection<ProductPriceHistory> PriceHistories { get; set; } = new List<ProductPriceHistory>();

    /// <summary>
    /// Navigation collection of tier prices across price levels.
    /// </summary>
    public ICollection<ProductPrice> ProductPrices { get; set; } = new List<ProductPrice>();
}
