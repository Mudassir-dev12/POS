using ShopPOS.Core.Enums;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Repositories;

public class SaleReturnRepository : Repository<SaleReturn>, ISaleReturnRepository
{
    public SaleReturnRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<SaleReturn?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Context.SaleReturns
            .Include(r => r.Items)
            .Include(r => r.Sale)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<List<SaleReturn>> GetReturnsForSaleAsync(int saleId, CancellationToken cancellationToken = default)
    {
        return await Context.SaleReturns
            .Include(r => r.Items)
            .Where(r => r.SaleId == saleId)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<SaleReturnDetailDto>> SearchSaleReturnsAsync(SaleReturnSearchDto criteria, CancellationToken cancellationToken = default)
    {
        var query = Context.SaleReturns
            .Include(r => r.Items)
            .Include(r => r.Sale)
            .AsNoTracking();

        if (criteria.Status.HasValue)
        {
            query = query.Where(r => r.Status == criteria.Status.Value);
        }

        if (criteria.SaleId.HasValue)
        {
            query = query.Where(r => r.SaleId == criteria.SaleId.Value);
        }

        if (criteria.DateFrom.HasValue)
        {
            query = query.Where(r => r.ReturnDate >= criteria.DateFrom.Value);
        }

        if (criteria.DateTo.HasValue)
        {
            query = query.Where(r => r.ReturnDate <= criteria.DateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.ReturnNumber.ToLower().Contains(term) ||
                (r.Sale != null && r.Sale.SaleNumber != null && r.Sale.SaleNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.ReturnDate)
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(r => new SaleReturnDetailDto
            {
                Id = r.Id,
                ReturnNumber = r.ReturnNumber,
                ReturnDate = r.ReturnDate,
                SaleId = r.SaleId,
                SaleNumber = r.Sale != null ? r.Sale.SaleNumber : null,
                Status = r.Status,
                SubTotal = r.SubTotal,
                TaxAmount = r.TaxAmount,
                TotalAmount = r.TotalAmount,
                RefundMethod = r.RefundMethod,
                RefundedAmount = r.RefundedAmount,
                Reason = r.Reason,
                CancelReason = r.CancelReason,
                Items = r.Items.Select(i => new SaleReturnItemDetailDto
                {
                    Id = i.Id,
                    SaleItemId = i.SaleItemId,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Sku = i.Sku,
                    Barcode = i.Barcode,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    CostPriceSnapshot = i.CostPriceSnapshot,
                    TaxPercent = i.TaxPercent,
                    LineTotal = i.LineTotal,
                    ReturnToStock = i.ReturnToStock
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SaleReturnDetailDto>(items, totalCount, criteria.PageNumber, criteria.PageSize);
    }

    public async Task<IReadOnlyList<int>> GetFilteredIdsAsync(SaleReturnSearchDto criteria, CancellationToken cancellationToken = default)
    {
        var query = Context.SaleReturns.AsNoTracking();

        if (criteria.Status.HasValue)
            query = query.Where(r => r.Status == criteria.Status.Value);

        if (criteria.SaleId.HasValue)
            query = query.Where(r => r.SaleId == criteria.SaleId.Value);

        if (criteria.DateFrom.HasValue)
            query = query.Where(r => r.ReturnDate >= criteria.DateFrom.Value);

        if (criteria.DateTo.HasValue)
            query = query.Where(r => r.ReturnDate <= criteria.DateTo.Value);

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.ReturnNumber.ToLower().Contains(term) ||
                (r.Reason != null && r.Reason.ToLower().Contains(term)));
        }

        return await query.Select(r => r.Id).ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetTotalReturnedQuantityForSaleItemAsync(int saleItemId, CancellationToken cancellationToken = default)
    {
        return await Context.SaleReturnItems
            .Where(i => i.SaleItemId == saleItemId && i.SaleReturn != null && i.SaleReturn.Status == SaleReturnStatus.Posted && !i.IsDeleted)
            .SumAsync(i => (decimal?)i.Quantity, cancellationToken) ?? 0m;
    }
}
