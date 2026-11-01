using ShopPOS.Core.DTOs;

namespace ShopPOS.Core.Interfaces;

public interface IProductLookupService
{
    (decimal Quantity, string Code) ParseBarcodeQuantity(string rawInput);
    Task<ProductLookupResult> LookupProductAsync(string rawInput, int? priceLevelId = null, CancellationToken cancellationToken = default);
}

