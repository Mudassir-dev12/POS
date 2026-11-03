using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShopPOS.Core.DTOs;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Provides high-speed multi-word searching across name, SKU, and barcodes with ranking and real-time stock computation.
/// </summary>
public interface IProductSearchService
{
    /// <summary>
    /// Searches active products with case-insensitive, multi-word matching anywhere in the name, SKU, or barcode.
    /// Ranked by: exact barcode/SKU, then name starts with, then contains.
    /// </summary>
    Task<IReadOnlyList<PosProductSearchResult>> SearchProductsAsync(
        string rawInput,
        int? priceLevelId = null,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses an optional quantity prefix (e.g. "5*cola" -> (5, "cola")).
    /// </summary>
    (decimal Quantity, string SearchTerm) ParseQuantityPrefix(string rawInput);
}

