using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Repositories;

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<ProductListItemDto>> GetPagedAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = Context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Unit)
            .AsQueryable();

        // 1. Filter by Active status
        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.IsActive == query.IsActive.Value);
        }

        // 2. Filter by Category
        if (query.CategoryId.HasValue && query.CategoryId.Value > 0)
        {
            baseQuery = baseQuery.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        // 3. Filter by Brand
        if (query.BrandId.HasValue && query.BrandId.Value > 0)
        {
            baseQuery = baseQuery.Where(p => p.BrandId == query.BrandId.Value);
        }

        // 4. Filter by Search Term (Name, Sku, Barcode, Alternate Barcodes)
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            baseQuery = baseQuery.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Sku.ToLower().Contains(term) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(term)) ||
                p.AdditionalBarcodes.Any(ab => ab.Barcode.ToLower().Contains(term)));
        }

        // 5. Filter by Low Stock / Out of Stock
        if (query.LowStockOnly)
        {
            baseQuery = baseQuery.Where(p =>
                p.TrackStock &&
                (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) > 0m &&
                (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) <= p.ReorderLevel);
        }
        else if (query.OutOfStockOnly)
        {
            baseQuery = baseQuery.Where(p =>
                p.TrackStock &&
                (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) <= 0m);
        }
        else if (query.OutOfStockOnly)
        {
            baseQuery = baseQuery.Where(p =>
                p.TrackStock &&
                (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) <= 0m);
        }

        // 6. Total count
        int totalCount = await baseQuery.CountAsync(cancellationToken);

        // 7. Sort
        baseQuery = query.SortBy?.ToLowerInvariant() switch
        {
            "sku" => query.SortDescending ? baseQuery.OrderByDescending(p => p.Sku) : baseQuery.OrderBy(p => p.Sku),
            "barcode" => query.SortDescending ? baseQuery.OrderByDescending(p => p.Barcode) : baseQuery.OrderBy(p => p.Barcode),
            "category" => query.SortDescending ? baseQuery.OrderByDescending(p => p.Category!.Name) : baseQuery.OrderBy(p => p.Category!.Name),
            "brand" => query.SortDescending ? baseQuery.OrderByDescending(p => p.Brand!.Name) : baseQuery.OrderBy(p => p.Brand!.Name),
            "costprice" => query.SortDescending ? baseQuery.OrderByDescending(p => p.CostPrice) : baseQuery.OrderBy(p => p.CostPrice),
            "saleprice" => query.SortDescending ? baseQuery.OrderByDescending(p => p.SalePrice) : baseQuery.OrderBy(p => p.SalePrice),
            "isactive" => query.SortDescending ? baseQuery.OrderByDescending(p => p.IsActive) : baseQuery.OrderBy(p => p.IsActive),
            _ => query.SortDescending ? baseQuery.OrderByDescending(p => p.Name) : baseQuery.OrderBy(p => p.Name)
        };

        // 8. Paging
        int pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
        int pageSize = query.PageSize > 0 ? query.PageSize : 50;

        var rawItems = await baseQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Barcode = p.Barcode ?? p.AdditionalBarcodes.Select(ab => ab.Barcode).FirstOrDefault(),
                Name = p.Name,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                BrandId = p.BrandId,
                BrandName = p.Brand != null ? p.Brand.Name : null,
                UnitId = p.UnitId,
                UnitName = p.Unit != null ? p.Unit.Name : string.Empty,
                UnitShortName = p.Unit != null ? p.Unit.ShortName : string.Empty,
                CostPrice = p.CostPrice,
                SalePrice = p.SalePrice,
                MinSalePrice = p.MinSalePrice,
                TaxPercent = p.TaxPercent,
                ReorderLevel = p.ReorderLevel,
                TrackStock = p.TrackStock,
                IsActive = p.IsActive,
                CurrentStock = 0m
            })
            .ToListAsync(cancellationToken);

        // 9. High-performance single grouped query for current stock calculation (O(1) roundtrip)
        if (rawItems.Count > 0)
        {
            var productIds = rawItems.Select(i => i.Id).ToList();
            var stockMap = await Context.StockMovements
                .AsNoTracking()
                .Where(sm => !sm.IsDeleted && productIds.Contains(sm.ProductId))
                .GroupBy(sm => sm.ProductId)
                .Select(g => new { ProductId = g.Key, Stock = g.Sum(sm => sm.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Stock, cancellationToken);

            foreach (var item in rawItems)
            {
                if (stockMap.TryGetValue(item.Id, out var s))
                {
                    item.CurrentStock = s;
                }
            }
        }

        return new PagedResult<ProductListItemDto>(rawItems, totalCount, pageNumber, pageSize);
    }

    public async Task<Product?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Unit)
            .Include(p => p.AdditionalBarcodes)
            .Include(p => p.ProductPrices).ThenInclude(pp => pp.PriceLevel)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetByIdsWithDetailsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        return await Context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Unit)
            .Include(p => p.AdditionalBarcodes)
            .Include(p => p.ProductPrices).ThenInclude(pp => pp.PriceLevel)
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> FindByBarcodeOrSkuWithDetailsAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var trimmed = code.Trim();

        var prod = await Context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Unit)
            .Include(p => p.AdditionalBarcodes)
            .Include(p => p.ProductPrices).ThenInclude(pp => pp.PriceLevel)
            .FirstOrDefaultAsync(p => p.Barcode == trimmed, cancellationToken);

        if (prod != null) return prod;

        prod = await Context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Unit)
            .Include(p => p.AdditionalBarcodes)
            .Include(p => p.ProductPrices).ThenInclude(pp => pp.PriceLevel)
            .FirstOrDefaultAsync(p => p.AdditionalBarcodes.Any(ab => ab.Barcode == trimmed), cancellationToken);

        if (prod != null) return prod;

        return await Context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Unit)
            .Include(p => p.AdditionalBarcodes)
            .Include(p => p.ProductPrices).ThenInclude(pp => pp.PriceLevel)
            .FirstOrDefaultAsync(p => p.Sku == trimmed, cancellationToken);
    }

    public async Task<bool> IsSkuExistsAsync(string sku, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = Context.Products.Where(p => p.Sku.ToLower() == sku.Trim().ToLower());
        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<ProductBarcodeConflict?> FindBarcodeConflictAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        var cleanBarcode = barcode.Trim();

        // 1. Check Product.Barcode
        var primaryConflict = await Context.Products
            .AsNoTracking()
            .Where(p => p.Barcode == cleanBarcode && (!excludeProductId.HasValue || p.Id != excludeProductId.Value))
            .Select(p => new ProductBarcodeConflict
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Sku = p.Sku,
                Barcode = cleanBarcode
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (primaryConflict != null)
        {
            return primaryConflict;
        }

        // 2. Check ProductBarcode table
        var additionalConflict = await Context.ProductBarcodes
            .AsNoTracking()
            .Include(pb => pb.Product)
            .Where(pb => pb.Barcode == cleanBarcode && (!excludeProductId.HasValue || pb.ProductId != excludeProductId.Value))
            .Select(pb => new ProductBarcodeConflict
            {
                ProductId = pb.ProductId,
                ProductName = pb.Product != null ? pb.Product.Name : "Unknown",
                Sku = pb.Product != null ? pb.Product.Sku : "Unknown",
                Barcode = cleanBarcode
            })
            .FirstOrDefaultAsync(cancellationToken);

        return additionalConflict;
    }

    public async Task<string> GenerateNextSkuAsync(CancellationToken cancellationToken = default)
    {
        // Find existing SKU numbers starting with 'P'
        var existingSkus = await Context.Products
            .AsNoTracking()
            .Where(p => p.Sku.StartsWith("P"))
            .Select(p => p.Sku)
            .ToListAsync(cancellationToken);

        long maxNum = 0;
        foreach (var sku in existingSkus)
        {
            var numberPart = sku.Substring(1);
            if (long.TryParse(numberPart, out var n) && n > maxNum)
            {
                maxNum = n;
            }
        }

        long next = maxNum + 1;
        string candidate = $"P{next:D6}";

        // Double check against any random custom SKU
        while (await Context.Products.AnyAsync(p => p.Sku == candidate, cancellationToken))
        {
            next++;
            candidate = $"P{next:D6}";
        }

        return candidate;
    }

    public async Task<int> GetReferenceCountAsync(int productId, CancellationToken cancellationToken = default)
    {
        int movements = await Context.StockMovements.CountAsync(sm => sm.ProductId == productId, cancellationToken);
        int sales = await Context.SaleItems.CountAsync(si => si.ProductId == productId, cancellationToken);
        int purchases = await Context.PurchaseItems.CountAsync(pi => pi.ProductId == productId, cancellationToken);
        return movements + sales + purchases;
    }

    public async Task<IReadOnlyList<int>> GetFilteredIdsAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = Context.Products
            .AsNoTracking()
            .AsQueryable();

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.IsActive == query.IsActive.Value);
        }

        if (query.CategoryId.HasValue && query.CategoryId.Value > 0)
        {
            baseQuery = baseQuery.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.BrandId.HasValue && query.BrandId.Value > 0)
        {
            baseQuery = baseQuery.Where(p => p.BrandId == query.BrandId.Value);
        }


        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Sku.ToLower().Contains(term) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(term)) ||
                p.AdditionalBarcodes.Any(b => b.Barcode.ToLower().Contains(term)));
        }

        return await baseQuery.Select(p => p.Id).ToListAsync(cancellationToken);
    }

    public async Task<Product> CreateWithOpeningStockAsync(Product product, decimal openingQuantity, decimal unitCost, int? userId, CancellationToken cancellationToken = default)
    {
        using var tx = await Context.Database.BeginTransactionAsync(cancellationToken);

        // 1. Add Product
        await Context.Products.AddAsync(product, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        // 2. Ensure Primary Barcode in ProductBarcode table if barcode is present
        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            var primaryBarcode = new ProductBarcode
            {
                ProductId = product.Id,
                Barcode = product.Barcode.Trim(),
                IsPrimary = true
            };
            await Context.ProductBarcodes.AddAsync(primaryBarcode, cancellationToken);
        }

        // 3. Insert Opening Stock Movement if quantity > 0 in the SAME transaction
        if (openingQuantity > 0)
        {
            var movement = new StockMovement
            {
                ProductId = product.Id,
                MovementType = StockMovementType.Opening,
                Quantity = openingQuantity,
                UnitCost = unitCost,
                Note = "Initial opening stock",
                CreatedByUserId = userId,
                MovementDate = DateTime.UtcNow
            };
            await Context.StockMovements.AddAsync(movement, cancellationToken);
        }

        await Context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return product;
    }

    public async Task<IReadOnlyList<ProductPriceHistoryDto>> GetPriceHistoriesAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await Context.ProductPriceHistories
            .AsNoTracking()
            .Include(h => h.ChangedByUser)
            .Where(h => h.ProductId == productId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new ProductPriceHistoryDto
            {
                Id = h.Id,
                ProductId = h.ProductId,
                OldCostPrice = h.OldCostPrice,
                NewCostPrice = h.NewCostPrice,
                OldSalePrice = h.OldSalePrice,
                NewSalePrice = h.NewSalePrice,
                ChangedByUserId = h.ChangedByUserId,
                ChangedByUserName = h.ChangedByUser != null ? h.ChangedByUser.FullName : "System",
                ChangedAt = h.ChangedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SyncProductBarcodesAsync(int productId, string? primaryBarcode, IEnumerable<string> additionalBarcodes, CancellationToken cancellationToken = default)
    {
        var existing = await Context.ProductBarcodes
            .Where(pb => pb.ProductId == productId)
            .ToListAsync(cancellationToken);

        Context.ProductBarcodes.RemoveRange(existing);

        var cleanPrimary = primaryBarcode?.Trim();
        if (!string.IsNullOrWhiteSpace(cleanPrimary))
        {
            await Context.ProductBarcodes.AddAsync(new ProductBarcode
            {
                ProductId = productId,
                Barcode = cleanPrimary,
                IsPrimary = true
            }, cancellationToken);
        }

        foreach (var code in additionalBarcodes)
        {
            var clean = code?.Trim();
            if (!string.IsNullOrWhiteSpace(clean) && !string.Equals(clean, cleanPrimary, StringComparison.OrdinalIgnoreCase))
            {
                await Context.ProductBarcodes.AddAsync(new ProductBarcode
                {
                    ProductId = productId,
                    Barcode = clean,
                    IsPrimary = false
                }, cancellationToken);
            }
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncProductPricesAsync(int productId, IEnumerable<ProductPriceDto> prices, CancellationToken cancellationToken = default)
    {
        var existing = await Context.ProductPrices
            .Where(pp => pp.ProductId == productId)
            .ToListAsync(cancellationToken);

        Context.ProductPrices.RemoveRange(existing);

        foreach (var price in prices)
        {
            if (price.PriceLevelId > 0 && price.Price > 0)
            {
                await Context.ProductPrices.AddAsync(new ProductPrice
                {
                    ProductId = productId,
                    PriceLevelId = price.PriceLevelId,
                    Price = price.Price
                }, cancellationToken);
            }
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordPriceChangeAsync(int productId, decimal oldCost, decimal newCost, decimal oldSale, decimal newSale, int? userId, CancellationToken cancellationToken = default)
    {
        if (oldCost != newCost || oldSale != newSale)
        {
            int? validUserId = null;
            if (userId.HasValue && await Context.Users.AnyAsync(u => u.Id == userId.Value, cancellationToken))
            {
                validUserId = userId.Value;
            }

            var history = new ProductPriceHistory
            {
                ProductId = productId,
                OldCostPrice = oldCost,
                NewCostPrice = newCost,
                OldSalePrice = oldSale,
                NewSalePrice = newSale,
                ChangedByUserId = validUserId,
                ChangedAt = DateTime.UtcNow
            };

            await Context.ProductPriceHistories.AddAsync(history, cancellationToken);
            await Context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RecordStockAdjustmentAsync(int productId, decimal quantityDelta, decimal unitCost, string? note, int? userId, CancellationToken cancellationToken = default)
    {
        if (quantityDelta == 0) return;

        int? validUserId = null;
        if (userId.HasValue && await Context.Users.AnyAsync(u => u.Id == userId.Value, cancellationToken))
        {
            validUserId = userId.Value;
        }

        var movement = new StockMovement
        {
            ProductId = productId,
            MovementType = StockMovementType.Adjustment,
            Quantity = quantityDelta,
            UnitCost = unitCost,
            ReferenceType = "ManualAdjustment",
            Note = string.IsNullOrWhiteSpace(note) ? "Manual stock adjustment" : note.Trim(),
            MovementDate = DateTime.UtcNow,
            CreatedByUserId = validUserId
        };

        await Context.StockMovements.AddAsync(movement, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal> GetCurrentStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await Context.StockMovements
            .AsNoTracking()
            .Where(sm => !sm.IsDeleted && sm.ProductId == productId)
            .SumAsync(sm => (decimal?)sm.Quantity, cancellationToken) ?? 0m;
    }

    public async Task<IReadOnlyList<Product>> GetAllForExportAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = Context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Unit)
            .Include(p => p.AdditionalBarcodes)
            .AsQueryable();

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.IsActive == query.IsActive.Value);
        }

        if (query.CategoryId.HasValue && query.CategoryId.Value > 0)
        {
            baseQuery = baseQuery.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.BrandId.HasValue && query.BrandId.Value > 0)
        {
            baseQuery = baseQuery.Where(p => p.BrandId == query.BrandId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            baseQuery = baseQuery.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Sku.ToLower().Contains(term) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(term)));
        }

        if (query.LowStockOnly)
        {
            baseQuery = baseQuery.Where(p =>
                p.TrackStock &&
                (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) > 0m &&
                (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) <= p.ReorderLevel);
        }
        else if (query.OutOfStockOnly)
        {
            baseQuery = baseQuery.Where(p =>
                p.TrackStock &&
                (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) <= 0m);
        }

        return await baseQuery.OrderBy(p => p.Name).ToListAsync(cancellationToken);
    }
}

