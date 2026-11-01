using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents an additional or alternate barcode for a product (e.g. single piece, pack, carton, or legacy code).
/// </summary>
public class ProductBarcode : BaseEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string Barcode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; } = false;
}

