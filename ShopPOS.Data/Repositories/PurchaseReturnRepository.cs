using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Repositories;

public class PurchaseReturnRepository : IPurchaseReturnRepository
{
    private readonly AppDbContext _context;

    public PurchaseReturnRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<PurchaseReturnDto>> GetReturnsAsync(int? supplierId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.PurchaseReturns
            .AsNoTracking()
            .Where(r => !r.IsDeleted);

        if (supplierId.HasValue && supplierId.Value > 0)
        {
            dbQuery = dbQuery.Where(r => r.SupplierId == supplierId.Value);
        }

        int totalCount = await dbQuery.CountAsync(cancellationToken);
        int page = Math.Max(1, pageNumber);
        int size = Math.Max(1, pageSize);

        var items = await dbQuery
            .OrderByDescending(r => r.ReturnDate)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(r => new PurchaseReturnDto
            {
                Id = r.Id,
                ReturnNumber = r.ReturnNumber,
                SupplierId = r.SupplierId,
                SupplierName = r.Supplier != null ? r.Supplier.Name : string.Empty,
                PurchaseId = r.PurchaseId,
                PurchaseNumber = r.Purchase != null ? r.Purchase.PurchaseNumber : null,
                ReturnDate = r.ReturnDate,
                TotalAmount = r.TotalAmount,
                Notes = r.Notes,
                Status = r.Status,
                Items = r.Items.Where(i => !i.IsDeleted).Select(i => new PurchaseReturnItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Sku = i.Sku,
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost,
                    LineTotal = i.LineTotal,
                    Note = i.Note
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseReturnDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = size
        };
    }

    public async Task<PurchaseReturn?> GetReturnByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseReturns
            .Include(r => r.Supplier)
            .Include(r => r.Purchase)
            .Include(r => r.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);
    }

    public async Task<PurchaseReturn> CreateReturnAsync(PurchaseReturn purchaseReturn, CancellationToken cancellationToken = default)
    {
        await _context.PurchaseReturns.AddAsync(purchaseReturn, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return purchaseReturn;
    }

    public async Task<decimal> GetTotalReturnedQuantityAsync(int purchaseId, int productId, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseReturnItems
            .Where(ri => !ri.IsDeleted &&
                         ri.PurchaseReturn!.PurchaseId == purchaseId &&
                         !ri.PurchaseReturn.IsDeleted &&
                         ri.ProductId == productId)
            .SumAsync(ri => (decimal?)ri.Quantity, cancellationToken) ?? 0m;
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

    public async Task<IReadOnlyList<int>> GetFilteredIdsAsync(int? supplierId, CancellationToken cancellationToken = default)
    {
        var query = _context.PurchaseReturns.AsNoTracking().Where(r => !r.IsDeleted);
        if (supplierId.HasValue && supplierId.Value > 0)
            query = query.Where(r => r.SupplierId == supplierId.Value);
        return await query.Select(r => r.Id).ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
