using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

public class StockLedgerQuery : IStockLedgerQuery
{
    private readonly AppDbContext _context;

    public StockLedgerQuery(AppDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetCurrentStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await _context.StockMovements
            .Where(sm => sm.ProductId == productId)
            .SumAsync(sm => (decimal?)sm.Quantity, cancellationToken) ?? 0m;
    }

    public async Task<Dictionary<int, decimal>> GetStockBatchAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default)
    {
        var idList = productIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<int, decimal>();

        Dictionary<int, decimal> grouped;
        if (idList.Count > 500)
        {
            grouped = new Dictionary<int, decimal>();
            foreach (var chunk in idList.Chunk(500))
            {
                var chunkGrouped = await _context.StockMovements
                    .Where(sm => chunk.Contains(sm.ProductId))
                    .GroupBy(sm => sm.ProductId)
                    .Select(g => new { ProductId = g.Key, Stock = g.Sum(sm => sm.Quantity) })
                    .ToDictionaryAsync(x => x.ProductId, x => x.Stock, cancellationToken);
                foreach (var kvp in chunkGrouped)
                {
                    grouped[kvp.Key] = kvp.Value;
                }
            }
        }
        else
        {
            grouped = await _context.StockMovements
                .Where(sm => idList.Contains(sm.ProductId))
                .GroupBy(sm => sm.ProductId)
                .Select(g => new { ProductId = g.Key, Stock = g.Sum(sm => sm.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Stock, cancellationToken);
        }

        // Ensure every requested ID exists in the map
        foreach (var id in idList)
        {
            if (!grouped.ContainsKey(id))
            {
                grouped[id] = 0m;
            }
        }

        return grouped;
    }

    public async Task<Dictionary<int, decimal>> GetAllStockMapAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StockMovements
            .GroupBy(sm => sm.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(sm => sm.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Stock, cancellationToken);
    }

    public async Task<bool> HasMovementsAfterAsync(int productId, DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        return await _context.StockMovements
            .AnyAsync(sm => sm.ProductId == productId && sm.CreatedAt > cutoffUtc, cancellationToken);
    }

    public async Task<Dictionary<int, bool>> CheckMovementsAfterBatchAsync(IEnumerable<int> productIds, DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var idList = productIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<int, bool>();

        var changedIds = await _context.StockMovements
            .Where(sm => idList.Contains(sm.ProductId) && sm.CreatedAt > cutoffUtc)
            .Select(sm => sm.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var changedSet = new HashSet<int>(changedIds);
        return idList.ToDictionary(id => id, id => changedSet.Contains(id));
    }
}

