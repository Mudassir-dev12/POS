namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Central contract for querying inventory stock levels and activity from the immutable StockMovements ledger.
/// Guarantees that all services read stock using a single shared logic.
/// </summary>
public interface IStockLedgerQuery
{
    /// <summary>
    /// Computes the current real-time ledger stock for a single product.
    /// </summary>
    Task<decimal> GetCurrentStockAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the current real-time ledger stock for a batch of products in a single grouped query.
    /// </summary>
    Task<Dictionary<int, decimal>> GetStockBatchAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the current real-time ledger stock map for all products in the catalog in a single grouped query.
    /// </summary>
    Task<Dictionary<int, decimal>> GetAllStockMapAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether any stock movements have been recorded for a product after the specified timestamp.
    /// Used by Stock Counts to detect activity during a counting session.
    /// </summary>
    Task<bool> HasMovementsAfterAsync(int productId, DateTime cutoffUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether any stock movements have been recorded after the specified timestamp for a batch of products.
    /// </summary>
    Task<Dictionary<int, bool>> CheckMovementsAfterBatchAsync(IEnumerable<int> productIds, DateTime cutoffUtc, CancellationToken cancellationToken = default);
}

