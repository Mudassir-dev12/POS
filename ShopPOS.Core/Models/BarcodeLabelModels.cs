using System.Collections.Generic;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Models;

/// <summary>
/// Physical label template layout.
/// </summary>
public enum BarcodeLabelLayout
{
    /// <summary>
    /// Standard 30-up A4 label sheet (3 columns x 10 rows, 70mm x 29.7mm per label).
    /// </summary>
    A4Sheet30Up,

    /// <summary>
    /// Direct thermal continuous roll label (50mm width x 30mm height).
    /// </summary>
    Thermal50x30,

    /// <summary>
    /// Compact jewelry/small item thermal roll label (40mm width x 25mm height).
    /// </summary>
    Thermal40x25
}

/// <summary>
/// Details of an individual product label to print.
/// </summary>
public class BarcodeLabelItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public BarcodeFormat Format { get; set; } = BarcodeFormat.Ean13;
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// Print request containing a batch of labels to format into a PDF document.
/// </summary>
public class BarcodeLabelRequest
{
    public string ShopName { get; set; } = "ShopPOS Store";
    public BarcodeLabelLayout Layout { get; set; } = BarcodeLabelLayout.A4Sheet30Up;
    public bool IncludeShopName { get; set; } = true;
    public bool IncludePrice { get; set; } = true;
    public bool IncludeProductName { get; set; } = true;
    public bool IncludeHumanReadableText { get; set; } = true;
    public List<BarcodeLabelItem> Items { get; set; } = new();
}
