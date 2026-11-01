using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly AppDbContext _context;

    public InventoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<StockOverviewItemDto>> GetStockOverviewAsync(StockOverviewFilterQuery query, bool isCashier, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .AsQueryable();

        if (!query.IncludeNonTracked && query.Status != StockStatus.NotTracked)
        {
            baseQuery = baseQuery.Where(p => p.TrackStock);
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
                (p.Barcode != null && p.Barcode.ToLower().Contains(term)) ||
                p.AdditionalBarcodes.Any(ab => ab.Barcode.ToLower().Contains(term)));
        }

        if (query.Status.HasValue)
        {
            switch (query.Status.Value)
            {
                case StockStatus.NotTracked:
                    baseQuery = baseQuery.Where(p => !p.TrackStock);
                    break;
                case StockStatus.Negative:
                    baseQuery = baseQuery.Where(p => p.TrackStock && (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) < 0m);
                    break;
                case StockStatus.OutOfStock:
                    baseQuery = baseQuery.Where(p => p.TrackStock && (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) == 0m);
                    break;
                case StockStatus.Low:
                    baseQuery = baseQuery.Where(p => p.TrackStock &&
                                                     (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) > 0m &&
                                                     (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) <= p.ReorderLevel);
                    break;
                case StockStatus.OK:
                    baseQuery = baseQuery.Where(p => p.TrackStock && (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) > p.ReorderLevel);
                    break;
            }
        }
        else if (query.LowOrOutOnly)
        {
            baseQuery = baseQuery.Where(p => p.TrackStock && (p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m) <= p.ReorderLevel);
        }

        int totalCount = await baseQuery.CountAsync(cancellationToken);
        int page = Math.Max(1, query.PageNumber);
        int size = Math.Max(1, query.PageSize);

        var pagedProducts = await baseQuery
            .OrderBy(p => p.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new
            {
                p.Id,
                p.Sku,
                p.Barcode,
                ProductName = p.Name,
                p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                p.BrandId,
                BrandName = p.Brand != null ? p.Brand.Name : null,
                UnitName = p.Unit != null ? p.Unit.Name : string.Empty,
                p.ReorderLevel,
                p.CostPrice,
                p.SalePrice,
                p.TrackStock,
                Stock = p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var items = pagedProducts.Select(p =>
        {
            decimal stock = p.Stock;
            StockStatus status;
            if (!p.TrackStock) status = StockStatus.NotTracked;
            else if (stock < 0) status = StockStatus.Negative;
            else if (stock == 0) status = StockStatus.OutOfStock;
            else if (stock <= p.ReorderLevel) status = StockStatus.Low;
            else status = StockStatus.OK;

            decimal? cost = isCashier ? null : p.CostPrice;
            decimal? costVal = isCashier ? null : Math.Round(stock * p.CostPrice, 2, MidpointRounding.AwayFromZero);
            decimal saleVal = Math.Round(stock * p.SalePrice, 2, MidpointRounding.AwayFromZero);

            return new StockOverviewItemDto
            {
                ProductId = p.Id,
                Sku = p.Sku,
                Barcode = p.Barcode,
                ProductName = p.ProductName,
                CategoryId = p.CategoryId,
                CategoryName = p.CategoryName,
                BrandId = p.BrandId,
                BrandName = p.BrandName,
                UnitName = p.UnitName,
                CurrentStock = stock,
                ReorderLevel = p.ReorderLevel,
                Status = status,
                CostPrice = cost,
                SalePrice = p.SalePrice,
                StockValueAtCost = costVal,
                StockValueAtSale = saleVal,
                TrackStock = p.TrackStock
            };
        }).ToList();

        return new PagedResult<StockOverviewItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = size
        };
    }

    public async Task<StockOverviewSummaryDto> GetStockOverviewSummaryAsync(bool isCashier, CancellationToken cancellationToken = default)
    {
        var activeStockProducts = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.TrackStock)
            .Select(p => new
            {
                p.Id,
                p.CostPrice,
                p.SalePrice,
                p.ReorderLevel,
                Stock = p.StockMovements.Where(sm => !sm.IsDeleted).Sum(sm => (decimal?)sm.Quantity) ?? 0m
            })
            .ToListAsync(cancellationToken);

        decimal totalUnits = 0m;
        decimal totalValueCost = 0m;
        decimal totalValueSale = 0m;
        int lowStock = 0;
        int outStock = 0;
        int negStock = 0;

        foreach (var p in activeStockProducts)
        {
            decimal stock = p.Stock;
            totalUnits += stock;
            if (!isCashier)
            {
                totalValueCost += stock * p.CostPrice;
            }
            totalValueSale += stock * p.SalePrice;

            if (stock < 0) negStock++;
            else if (stock == 0) outStock++;
            else if (stock <= p.ReorderLevel) lowStock++;
        }

        return new StockOverviewSummaryDto
        {
            TotalUnits = totalUnits,
            TotalValueAtCost = isCashier ? null : Math.Round(totalValueCost, 2, MidpointRounding.AwayFromZero),
            TotalValueAtSale = Math.Round(totalValueSale, 2, MidpointRounding.AwayFromZero),
            LowStockCount = lowStock,
            OutOfStockCount = outStock,
            NegativeStockCount = negStock,
            TotalProductCount = activeStockProducts.Count
        };
    }

    public async Task<PagedResult<MovementHistoryItemDto>> GetMovementHistoryAsync(MovementHistoryFilterQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.StockMovements
            .AsNoTracking()
            .Include(sm => sm.Product)
            .AsQueryable();

        if (query.ProductId.HasValue && query.ProductId.Value > 0)
        {
            dbQuery = dbQuery.Where(sm => sm.ProductId == query.ProductId.Value);
        }

        if (query.MovementType.HasValue)
        {
            dbQuery = dbQuery.Where(sm => sm.MovementType == query.MovementType.Value);
        }

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.Date;
            dbQuery = dbQuery.Where(sm => sm.MovementDate >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            dbQuery = dbQuery.Where(sm => sm.MovementDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.ReferenceSearch))
        {
            var term = query.ReferenceSearch.Trim().ToLower();
            dbQuery = dbQuery.Where(sm =>
                (sm.ReferenceType != null && sm.ReferenceType.ToLower().Contains(term)) ||
                (sm.Note != null && sm.Note.ToLower().Contains(term)));
        }

        int totalCount = await dbQuery.CountAsync(cancellationToken);
        int page = Math.Max(1, query.PageNumber);
        int size = Math.Max(1, query.PageSize);

        // If a single product is selected, we calculate running balances
        if (query.ProductId.HasValue && query.ProductId.Value > 0)
        {
            // Fetch all movements for this product in chronological order to compute running balance accurately
            var allProductMovements = await _context.StockMovements
                .AsNoTracking()
                .Where(sm => sm.ProductId == query.ProductId.Value)
                .OrderBy(sm => sm.MovementDate)
                .ThenBy(sm => sm.Id)
                .Select(sm => new { sm.Id, sm.Quantity })
                .ToListAsync(cancellationToken);

            decimal running = 0m;
            var balanceMap = new Dictionary<int, decimal>();
            foreach (var m in allProductMovements)
            {
                running += m.Quantity;
                balanceMap[m.Id] = running;
            }

            var pagedEntities = await dbQuery
                .OrderByDescending(sm => sm.MovementDate)
                .ThenByDescending(sm => sm.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            var items = pagedEntities.Select(sm =>
            {
                var effectiveDate = (sm.MovementDate.TimeOfDay == TimeSpan.Zero && sm.CreatedAt.TimeOfDay != TimeSpan.Zero)
                    ? sm.MovementDate.Date.Add(sm.CreatedAt.ToLocalTime().TimeOfDay)
                    : sm.MovementDate;

                return new MovementHistoryItemDto
                {
                    Id = sm.Id,
                    MovementDate = effectiveDate,
                    ProductId = sm.ProductId,
                    ProductName = sm.Product?.Name ?? string.Empty,
                    Sku = sm.Product?.Sku ?? string.Empty,
                    MovementType = sm.MovementType,
                    Quantity = sm.Quantity,
                    UnitCost = sm.UnitCost,
                    ReferenceType = sm.ReferenceType,
                    ReferenceId = sm.ReferenceId,
                    ReferenceNumber = sm.ReferenceType != null && sm.ReferenceId != null ? $"{sm.ReferenceType} #{sm.ReferenceId}" : null,
                    Note = sm.Note,
                    RunningBalance = balanceMap.TryGetValue(sm.Id, out var bal) ? bal : null
                };
            }).ToList();

            return new PagedResult<MovementHistoryItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = size
            };
        }
        else
        {
            var pagedEntities = await dbQuery
                .OrderByDescending(sm => sm.MovementDate)
                .ThenByDescending(sm => sm.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            var items = pagedEntities.Select(sm =>
            {
                var effectiveDate = (sm.MovementDate.TimeOfDay == TimeSpan.Zero && sm.CreatedAt.TimeOfDay != TimeSpan.Zero)
                    ? sm.MovementDate.Date.Add(sm.CreatedAt.ToLocalTime().TimeOfDay)
                    : sm.MovementDate;

                return new MovementHistoryItemDto
                {
                    Id = sm.Id,
                    MovementDate = effectiveDate,
                    ProductId = sm.ProductId,
                    ProductName = sm.Product?.Name ?? string.Empty,
                    Sku = sm.Product?.Sku ?? string.Empty,
                    MovementType = sm.MovementType,
                    Quantity = sm.Quantity,
                    UnitCost = sm.UnitCost,
                    ReferenceType = sm.ReferenceType,
                    ReferenceId = sm.ReferenceId,
                    ReferenceNumber = sm.ReferenceType != null && sm.ReferenceId != null ? $"{sm.ReferenceType} #{sm.ReferenceId}" : null,
                    Note = sm.Note,
                    RunningBalance = null
                };
            }).ToList();

            return new PagedResult<MovementHistoryItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = size
            };
        }
    }

    public async Task<IReadOnlyList<MovementHistoryItemDto>> GetAllMovementHistoryAsync(MovementHistoryFilterQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.StockMovements
            .AsNoTracking()
            .Include(sm => sm.Product)
            .AsQueryable();

        if (query.ProductId.HasValue && query.ProductId.Value > 0)
        {
            dbQuery = dbQuery.Where(sm => sm.ProductId == query.ProductId.Value);
        }

        if (query.MovementType.HasValue)
        {
            dbQuery = dbQuery.Where(sm => sm.MovementType == query.MovementType.Value);
        }

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.Date;
            dbQuery = dbQuery.Where(sm => sm.MovementDate >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            dbQuery = dbQuery.Where(sm => sm.MovementDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.ReferenceSearch))
        {
            var term = query.ReferenceSearch.Trim().ToLower();
            dbQuery = dbQuery.Where(sm =>
                (sm.ReferenceType != null && sm.ReferenceType.ToLower().Contains(term)) ||
                (sm.Note != null && sm.Note.ToLower().Contains(term)));
        }

        var entities = await dbQuery
            .OrderByDescending(sm => sm.MovementDate)
            .ThenByDescending(sm => sm.Id)
            .Take(10000)
            .ToListAsync(cancellationToken);

        return entities.Select(sm =>
        {
            var effectiveDate = (sm.MovementDate.TimeOfDay == TimeSpan.Zero && sm.CreatedAt.TimeOfDay != TimeSpan.Zero)
                ? sm.MovementDate.Date.Add(sm.CreatedAt.ToLocalTime().TimeOfDay)
                : sm.MovementDate;

            return new MovementHistoryItemDto
            {
                Id = sm.Id,
                MovementDate = effectiveDate,
                ProductId = sm.ProductId,
                ProductName = sm.Product?.Name ?? string.Empty,
                Sku = sm.Product?.Sku ?? string.Empty,
                MovementType = sm.MovementType,
                Quantity = sm.Quantity,
                UnitCost = sm.UnitCost,
                ReferenceType = sm.ReferenceType,
                ReferenceId = sm.ReferenceId,
                ReferenceNumber = sm.ReferenceType != null && sm.ReferenceId != null ? $"{sm.ReferenceType} #{sm.ReferenceId}" : null,
                Note = sm.Note,
                RunningBalance = null
            };
        }).ToList();
    }

    public async Task<(int purchasesCount, decimal purchasesTotal)> GetPurchasesThisMonthStatsAsync(CancellationToken cancellationToken = default)
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var purchases = await _context.Purchases
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Status == PurchaseStatus.Posted && p.PurchaseDate >= startOfMonth)
            .ToListAsync(cancellationToken);

        return (purchases.Count, purchases.Sum(p => p.GrandTotal));
    }
}
