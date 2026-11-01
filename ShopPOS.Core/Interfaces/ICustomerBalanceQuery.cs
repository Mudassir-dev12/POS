namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Computes real-time dynamic balance owed by customers from the ledger.
/// Formula: Customer.OpeningBalance + Sum(DueAmount of Completed non-cancelled Sales) - Sum(TotalAmount of Posted StoreCredit returns) - [Phase 6: CustomerPayments].
/// </summary>
public interface ICustomerBalanceQuery
{
    Task<decimal> GetCustomerBalanceAsync(int customerId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, decimal>> GetCustomerBalancesBatchAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default);
}

