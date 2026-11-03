using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

public class ProductSearchService : IProductSearchService
{
    private readonly AppDbContext _context;
    private readonly IStockLedgerQuery _stockLedgerQuery;

    public ProductSearchService(AppDbContext context, IStockLedgerQuery stockLedgerQuery)
    {
        _context = context;
        _stockLedgerQuery = stockLedgerQuery;
    }

    public (decimal Quantity, string SearchTerm) ParseQuantityPrefix(string rawInput)
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
            var searchPart = trimmed.Substring(asteriskIndex + 1).Trim();

            if (decimal.TryParse(qtyPart, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) && qty > 0m)
            {
                return (qty, searchPart);
            }
        }

        return (1m, trimmed);
    }

    public async Task<IReadOnlyList<PosProductSearchResult>> SearchProductsAsync(
        string rawInput,
        int? priceLevelId = null,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var (quantity, searchTerm) = ParseQuantityPrefix(rawInput);
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Array.Empty<PosProductSearchResult>();
        }

        var normalizedQuery = searchTerm.Trim().ToLowerInvariant();
        var words = normalizedQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
        {
            return Array.Empty<PosProductSearchResult>();
        }

        // Query active, non-deleted products
        var baseQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted);

        // Every word must be matched in SearchText (or Name/Sku/Barcode fallback)
        foreach (var word in words)
        {
            var w = word;
            baseQuery = baseQuery.Where(p => p.SearchText.Contains(w));
        }

        // Fetch candidates with needed details (take up to 100 to allow ranking in memory before capping to maxResults)
        var candidates = await baseQuery
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.SalePrice,
                p.ReorderLevel,
                p.TrackStock,
                UnitName = p.Unit != null ? p.Unit.Name : string.Empty,
                UnitShortName = p.Unit != null ? p.Unit.ShortName : string.Empty,
                AdditionalBarcodes = p.AdditionalBarcodes.Where(ab => !ab.IsDeleted).Select(ab => ab.Barcode).ToList(),
                TierPrices = p.ProductPrices.Where(pp => !pp.IsDeleted).Select(pp => new { pp.PriceLevelId, pp.Price }).ToList()
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return Array.Empty<PosProductSearchResult>();
        }

        // Compute rankings
        // 1: Exact barcode or SKU match
        // 2: Name starts with query text
        // 3: Name contains query text
        // 4: Other (matched multi-word across fields)
        var ranked = candidates.Select(c =>
        {
            int rank = 4;
            bool isExactBarcodeOrSku = string.Equals(c.Sku, normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(c.Barcode, normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                                       c.AdditionalBarcodes.Any(b => string.Equals(b, normalizedQuery, StringComparison.OrdinalIgnoreCase));

            if (isExactBarcodeOrSku)
            {
                rank = 1;
            }
            else if (c.Name.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                rank = 2;
            }
            else if (c.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                rank = 3;
            }

            // Resolve price for tier if selected
            decimal price = c.SalePrice;
            if (priceLevelId.HasValue)
            {
                var tier = c.TierPrices.FirstOrDefault(tp => tp.PriceLevelId == priceLevelId.Value);
                if (tier != null)
                {
                    price = tier.Price;
                }
            }

            return new
            {
                Candidate = c,
                Rank = rank,
                ResolvedPrice = price
            };
        })
        .OrderBy(x => x.Rank)
        .ThenBy(x => x.Candidate.Name)
        .Take(maxResults)
        .ToList();

        // Compute stock from ledger for top results
        var productIds = ranked.Select(r => r.Candidate.Id).Distinct().ToList();
        var stockMap = await _stockLedgerQuery.GetStockBatchAsync(productIds, cancellationToken);

        var results = new List<PosProductSearchResult>(ranked.Count);
        foreach (var item in ranked)
        {
            var c = item.Candidate;
            decimal stock = stockMap.TryGetValue(c.Id, out var s) ? s : 0m;

            StockStatus status;
            if (!c.TrackStock) status = StockStatus.NotTracked;
            else if (stock < 0) status = StockStatus.Negative;
            else if (stock == 0) status = StockStatus.OutOfStock;
            else if (stock <= c.ReorderLevel) status = StockStatus.Low;
            else status = StockStatus.OK;

            results.Add(new PosProductSearchResult
            {
                ProductId = c.Id,
                Name = c.Name,
                Sku = c.Sku,
                Barcode = c.Barcode ?? c.AdditionalBarcodes.FirstOrDefault(),
                SalePrice = item.ResolvedPrice,
                CurrentStock = stock,
                ReorderLevel = c.ReorderLevel,
                TrackStock = c.TrackStock,
                StockStatus = status,
                UnitName = c.UnitName,
                UnitShortName = c.UnitShortName,
                ParsedQuantity = quantity,
                MatchRank = item.Rank
            });
        }

        return results;
    }
}

