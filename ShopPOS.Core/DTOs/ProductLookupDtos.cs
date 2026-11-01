using ShopPOS.Core.Entities;

namespace ShopPOS.Core.DTOs;

public class ProductLookupResult
{
    public bool Found { get; set; }
    public Product? Product { get; set; }
    public string? MatchedBarcode { get; set; }
    public decimal ParsedQuantity { get; set; } = 1m;
    public decimal ResolvedUnitPrice { get; set; }
    public string? ErrorMessage { get; set; }
}

