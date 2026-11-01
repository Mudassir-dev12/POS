using System.Globalization;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Core.Services;

public class ProductLookupService : IProductLookupService
{
    private readonly IProductRepository _productRepository;

    public ProductLookupService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public (decimal Quantity, string Code) ParseBarcodeQuantity(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return (1m, string.Empty);
        }

        var trimmed = rawInput.Trim();
        var asteriskIndex = trimmed.IndexOf('*');

        if (asteriskIndex > 0 && asteriskIndex < trimmed.Length - 1)
        {
            var qtyPart = trimmed.Substring(0, asteriskIndex).Trim();
            var codePart = trimmed.Substring(asteriskIndex + 1).Trim();

            if (decimal.TryParse(qtyPart, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) && qty > 0m)
            {
                return (qty, codePart);
            }
        }

        return (1m, trimmed);
    }

    public async Task<ProductLookupResult> LookupProductAsync(string rawInput, int? priceLevelId = null, CancellationToken cancellationToken = default)
    {
        var (quantity, code) = ParseBarcodeQuantity(rawInput);

        if (string.IsNullOrWhiteSpace(code))
        {
            return new ProductLookupResult
            {
                Found = false,
                ErrorMessage = "Product barcode or SKU cannot be empty."
            };
        }

        var product = await _productRepository.FindByBarcodeOrSkuWithDetailsAsync(code, cancellationToken);

        if (product == null)
        {
            return new ProductLookupResult
            {
                Found = false,
                ErrorMessage = $"No product found matching code '{code}'."
            };
        }

        if (!product.IsActive)
        {
            return new ProductLookupResult
            {
                Found = false,
                ErrorMessage = $"Product '{product.Name}' is inactive."
            };
        }

        // Determine which barcode matched
        string? matchedBarcode = null;
        if (string.Equals(product.Barcode, code, StringComparison.OrdinalIgnoreCase))
        {
            matchedBarcode = product.Barcode;
        }
        else if (product.AdditionalBarcodes != null)
        {
            var sec = product.AdditionalBarcodes.FirstOrDefault(ab => string.Equals(ab.Barcode, code, StringComparison.OrdinalIgnoreCase));
            if (sec != null)
            {
                matchedBarcode = sec.Barcode;
            }
        }

        if (matchedBarcode == null)
        {
            matchedBarcode = product.Barcode;
        }

        // Resolve unit price based on customer's price level tier if present
        decimal unitPrice = product.SalePrice;
        if (priceLevelId.HasValue && product.ProductPrices != null)
        {
            var tier = product.ProductPrices.FirstOrDefault(pp => pp.PriceLevelId == priceLevelId.Value);
            if (tier != null)
            {
                unitPrice = tier.Price;
            }
        }

        return new ProductLookupResult
        {
            Found = true,
            Product = product,
            MatchedBarcode = matchedBarcode,
            ParsedQuantity = quantity,
            ResolvedUnitPrice = unitPrice
        };
    }
}
