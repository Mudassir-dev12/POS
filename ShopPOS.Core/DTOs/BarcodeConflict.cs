namespace ShopPOS.Core.DTOs;

public class ProductBarcodeConflict
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
}

