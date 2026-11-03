using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs;

/// <summary>
/// Detailed result returned by high-speed POS search for suggestions and F2 search dialog.
/// </summary>
public class PosProductSearchResult
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal SalePrice { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool TrackStock { get; set; }
    public StockStatus StockStatus { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitShortName { get; set; } = string.Empty;
    public decimal ParsedQuantity { get; set; } = 1m;
    public int MatchRank { get; set; }

    public string StockStatusDisplay => StockStatus switch
    {
        StockStatus.OK => "OK",
        StockStatus.Low => "Low stock",
        StockStatus.OutOfStock => "Out of stock",
        StockStatus.Negative => "Negative",
        StockStatus.NotTracked => "Not tracked",
        _ => StockStatus.ToString()
    };
}

