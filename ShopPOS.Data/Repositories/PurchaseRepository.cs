using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;

namespace ShopPOS.Data.Repositories;

public class PurchaseRepository : IPurchaseRepository
{
    private readonly AppDbContext _context;

    public PurchaseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<PurchaseListDto>> GetPurchasesAsync(PurchaseSearchQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Purchases
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            dbQuery = dbQuery.Where(p =>
                p.PurchaseNumber.ToLower().Contains(term) ||
                (p.SupplierInvoiceNo != null && p.SupplierInvoiceNo.ToLower().Contains(term)) ||
                (p.Supplier != null && p.Supplier.Name.ToLower().Contains(term)));
        }

        if (query.SupplierId.HasValue && query.SupplierId.Value > 0)
        {
            dbQuery = dbQuery.Where(p => p.SupplierId == query.SupplierId.Value);
        }

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.Date;
            dbQuery = dbQuery.Where(p => p.PurchaseDate >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            dbQuery = dbQuery.Where(p => p.PurchaseDate <= to);
        }

        // Project directly to DTO
        var projectionQuery = dbQuery
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.Id)
            .Select(p => new PurchaseListDto
            {
                Id = p.Id,
                PurchaseNumber = p.PurchaseNumber,
                PurchaseDate = p.PurchaseDate,
                SupplierId = p.SupplierId,
                SupplierName = p.Supplier != null ? p.Supplier.Name : string.Empty,
                SupplierInvoiceNo = p.SupplierInvoiceNo,
                GrandTotal = p.GrandTotal,
                PaidAmount = p.PaidAmount,
                Status = p.Status,
                DueDate = p.DueDate,
                ItemCount = p.Items.Count(i => !i.IsDeleted)
            });

        var allItems = await projectionQuery.ToListAsync(cancellationToken);

        // Compute payment status in memory
        foreach (var item in allItems)
        {
            item.PaymentStatus = PurchaseCalculator.DeterminePaymentStatus(
                item.GrandTotal,
                item.PaidAmount,
                item.DueDate,
                item.Status);
        }

        // Apply payment status filter if specified
        if (query.PaymentStatus.HasValue)
        {
            allItems = allItems.Where(i => i.PaymentStatus == query.PaymentStatus.Value).ToList();
        }

        int totalCount = allItems.Count;
        int pageNumber = Math.Max(1, query.PageNumber);
        int pageSize = Math.Max(1, query.PageSize);

        var pagedItems = allItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<PurchaseListDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Purchase?> GetPurchaseByIdAsync(int id, bool includeItems = true, CancellationToken cancellationToken = default)
    {
        var q = _context.Purchases
            .Include(p => p.Supplier)
            .AsQueryable();

        if (includeItems)
        {
            q = q.Include(p => p.Items.Where(i => !i.IsDeleted))
                 .ThenInclude(i => i.Product);
        }

        return await q.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public async Task<PurchaseDetailDto?> GetPurchaseDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var p = await _context.Purchases
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.PostedByUser)
            .Include(x => x.CancelledByUser)
            .Include(x => x.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (p == null) return null;

        var detail = new PurchaseDetailDto
        {
            Id = p.Id,
            PurchaseNumber = p.PurchaseNumber,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier?.Name ?? string.Empty,
            SupplierInvoiceNo = p.SupplierInvoiceNo,
            PurchaseDate = p.PurchaseDate,
            DueDate = p.DueDate,
            Status = p.Status,
            SubTotal = p.SubTotal,
            DiscountAmount = p.DiscountAmount,
            TaxAmount = p.TaxAmount,
            FreightAmount = p.FreightAmount,
            GrandTotal = p.GrandTotal,
            PaidAmount = p.PaidAmount,
            Notes = p.Notes,
            PostedAt = p.PostedAt,
            PostedByName = p.PostedByUser?.FullName,
            CancelledAt = p.CancelledAt,
            CancelledByName = p.CancelledByUser?.FullName,
            CancelReason = p.CancelReason,
            Items = p.Items.Select(i => new PurchaseItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Sku = i.Sku,
                Barcode = i.Product?.Barcode,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost,
                DiscountPercent = i.DiscountPercent,
                TaxPercent = i.TaxPercent,
                LineTotal = i.LineTotal,
                Note = i.Note
            }).ToList()
        };

        detail.PaymentStatus = PurchaseCalculator.DeterminePaymentStatus(
            detail.GrandTotal,
            detail.PaidAmount,
            detail.DueDate,
            detail.Status);

        return detail;
    }

    public async Task<Purchase> CreatePurchaseAsync(Purchase purchase, CancellationToken cancellationToken = default)
    {
        await _context.Purchases.AddAsync(purchase, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return purchase;
    }

    public async Task UpdatePurchaseAsync(Purchase purchase, CancellationToken cancellationToken = default)
    {
        _context.Purchases.Update(purchase);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeletePurchaseAsync(int id, CancellationToken cancellationToken = default)
    {
        var purchase = await _context.Purchases
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (purchase != null)
        {
            purchase.IsDeleted = true;
            foreach (var item in purchase.Items)
            {
                item.IsDeleted = true;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> IsSupplierInvoiceNoUsedAsync(int supplierId, string supplierInvoiceNo, int? excludePurchaseId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supplierInvoiceNo)) return false;

        var term = supplierInvoiceNo.Trim().ToLower();
        return await _context.Purchases.AnyAsync(p =>
            !p.IsDeleted &&
            p.SupplierId == supplierId &&
            p.SupplierInvoiceNo != null &&
            p.SupplierInvoiceNo.ToLower() == term &&
            (!excludePurchaseId.HasValue || p.Id != excludePurchaseId.Value),
            cancellationToken);
    }

    public async Task<decimal> GetProductCurrentStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await _context.StockMovements
            .Where(sm => !sm.IsDeleted && sm.ProductId == productId)
            .SumAsync(sm => (decimal?)sm.Quantity, cancellationToken) ?? 0m;
    }

    public async Task<Dictionary<int, decimal>> GetProductsCurrentStockAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default)
    {
        var idList = productIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<int, decimal>();

        var map = await _context.StockMovements
            .AsNoTracking()
            .Where(sm => !sm.IsDeleted && idList.Contains(sm.ProductId))
            .GroupBy(sm => sm.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(sm => sm.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Stock, cancellationToken);

        foreach (var id in idList)
        {
            if (!map.ContainsKey(id)) map[id] = 0m;
        }

        return map;
    }

    public async Task<decimal> GetProductLastCostAsync(int productId, CancellationToken cancellationToken = default)
    {
        // Try getting last purchase line cost
        var lastPurchaseCost = await _context.PurchaseItems
            .AsNoTracking()
            .Where(pi => !pi.IsDeleted && pi.ProductId == productId && pi.Purchase!.Status == PurchaseStatus.Posted)
            .OrderByDescending(pi => pi.Purchase!.PurchaseDate)
            .ThenByDescending(pi => pi.Id)
            .Select(pi => (decimal?)pi.UnitCost)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastPurchaseCost.HasValue) return lastPurchaseCost.Value;

        // Fallback to Product.CostPrice
        return await _context.Products
            .Where(p => p.Id == productId)
            .Select(p => p.CostPrice)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Dictionary<int, decimal>> GetProductsLastCostAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default)
    {
        var idList = productIds.Distinct().ToList();
        var result = new Dictionary<int, decimal>();

        foreach (var id in idList)
        {
            result[id] = await GetProductLastCostAsync(id, cancellationToken);
        }

        return result;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetProductsByIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default)
    {
        var idList = productIds.Distinct().ToList();
        return await _context.Products.Where(p => idList.Contains(p.Id)).ToListAsync(cancellationToken);
    }

    public async Task AddStockMovementAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        await _context.StockMovements.AddAsync(movement, cancellationToken);
    }

    public async Task AddSupplierPaymentAsync(SupplierPayment payment, CancellationToken cancellationToken = default)
    {
        await _context.SupplierPayments.AddAsync(payment, cancellationToken);
    }

    public async Task UpdateProductCostPriceAsync(int productId, decimal newCostPrice, decimal oldCostPrice, int? userId, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product != null)
        {
            if (newCostPrice != oldCostPrice)
            {
                var history = new ProductPriceHistory
                {
                    ProductId = productId,
                    OldCostPrice = oldCostPrice,
                    NewCostPrice = newCostPrice,
                    OldSalePrice = product.SalePrice,
                    NewSalePrice = product.SalePrice,
                    ChangedByUserId = userId,
                    ChangedAt = DateTime.UtcNow
                };
                await _context.ProductPriceHistories.AddAsync(history, cancellationToken);
            }
            product.CostPrice = newCostPrice;
        }
    }

    public async Task<bool> HasActivePaymentsForPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        return await _context.SupplierPayments.AnyAsync(p => !p.IsDeleted && !p.IsCancelled && p.PurchaseId == purchaseId, cancellationToken);
    }

    public async Task<bool> HasActiveReturnsForPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseReturns.AnyAsync(r => !r.IsDeleted && r.PurchaseId == purchaseId && r.Status == PurchaseReturnStatus.Posted, cancellationToken);
    }

    public async Task<decimal> GetTotalReturnedForPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseReturns
            .Where(r => !r.IsDeleted && r.PurchaseId == purchaseId && r.Status == PurchaseReturnStatus.Posted)
            .SumAsync(r => (decimal?)r.TotalAmount, cancellationToken) ?? 0m;
    }
}
