using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Repositories;

public class StockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly AppDbContext _context;

    public StockAdjustmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<StockAdjustmentListDto>> GetAdjustmentsAsync(DateTime? fromDate, DateTime? toDate, StockAdjustmentStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.StockAdjustments
            .AsNoTracking()
            .Where(a => !a.IsDeleted);

        if (fromDate.HasValue)
        {
            var start = fromDate.Value.Date;
            dbQuery = dbQuery.Where(a => a.AdjustmentDate >= start);
        }

        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
            dbQuery = dbQuery.Where(a => a.AdjustmentDate <= end);
        }

        if (status.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.Status == status.Value);
        }

        int totalCount = await dbQuery.CountAsync(cancellationToken);
        int page = Math.Max(1, pageNumber);
        int size = Math.Max(1, pageSize);

        var items = await dbQuery
            .OrderByDescending(a => a.AdjustmentDate)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(a => new StockAdjustmentListDto
            {
                Id = a.Id,
                AdjustmentNumber = a.AdjustmentNumber,
                AdjustmentDate = a.AdjustmentDate,
                Reason = a.Reason,
                ItemCount = a.Items.Count(i => !i.IsDeleted),
                TotalValueDifference = a.Items.Where(i => !i.IsDeleted).Sum(i => i.Difference * i.UnitCost),
                Status = a.Status,
                Notes = a.Notes
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<StockAdjustmentListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = size
        };
    }

    public async Task<StockAdjustment?> GetAdjustmentByIdAsync(int id, bool includeItems = true, CancellationToken cancellationToken = default)
    {
        var query = _context.StockAdjustments.AsQueryable();
        if (includeItems)
        {
            query = query
                .Include(a => a.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.Product)
                .Include(a => a.PostedByUser)
                .Include(a => a.CancelledByUser);
        }

        return await query.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);
    }

    public async Task<StockAdjustmentDetailDto?> GetAdjustmentDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var adjustment = await _context.StockAdjustments
            .AsNoTracking()
            .Include(a => a.PostedByUser)
            .Include(a => a.CancelledByUser)
            .Include(a => a.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);

        if (adjustment == null) return null;

        return new StockAdjustmentDetailDto
        {
            Id = adjustment.Id,
            AdjustmentNumber = adjustment.AdjustmentNumber,
            AdjustmentDate = adjustment.AdjustmentDate,
            Reason = adjustment.Reason,
            Notes = adjustment.Notes,
            Status = adjustment.Status,
            PostedAt = adjustment.PostedAt,
            PostedByName = adjustment.PostedByUser?.FullName ?? adjustment.PostedByUser?.Username,
            CancelledAt = adjustment.CancelledAt,
            CancelledByName = adjustment.CancelledByUser?.FullName ?? adjustment.CancelledByUser?.Username,
            CancelReason = adjustment.CancelReason,
            Items = adjustment.Items.Select(i => new StockAdjustmentItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Sku = i.Sku,
                SystemQuantity = i.SystemQuantity,
                NewQuantity = i.NewQuantity,
                Difference = i.Difference,
                UnitCost = i.UnitCost,
                Note = i.Note
            }).ToList()
        };
    }

    public async Task<StockAdjustment> CreateAdjustmentAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        await _context.StockAdjustments.AddAsync(adjustment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return adjustment;
    }

    public async Task UpdateAdjustmentAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        _context.StockAdjustments.Update(adjustment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAdjustmentAsync(int id, CancellationToken cancellationToken = default)
    {
        var adj = await _context.StockAdjustments.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);
        if (adj != null)
        {
            adj.IsDeleted = true;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddStockMovementAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        await _context.StockMovements.AddAsync(movement, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

