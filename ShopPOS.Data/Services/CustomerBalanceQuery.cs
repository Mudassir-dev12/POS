using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Computes dynamic customer balance owed from the ledger in real-time.
/// Formula: OpeningBalance + Sum(DueAmount of Completed non-cancelled Sales) - Sum(RefundedAmount of Posted StoreCredit Returns).
/// [Phase 6 Extension Point: Minus Sum(CustomerPayments)].
/// </summary>
public class CustomerBalanceQuery : ICustomerBalanceQuery
{
    private readonly AppDbContext _context;

    public CustomerBalanceQuery(AppDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetCustomerBalanceAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var openingBalance = await _context.Customers
            .Where(c => c.Id == customerId)
            .Select(c => (decimal?)c.OpeningBalance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

        var salesDue = await _context.Sales
            .Where(s => s.CustomerId == customerId && s.Status == SaleStatus.Completed)
            .SumAsync(s => (decimal?)s.DueAmount, cancellationToken) ?? 0m;

        var storeCreditRefunds = await _context.SaleReturns
            .Where(r => r.CustomerId == customerId && r.Status == SaleReturnStatus.Posted && r.RefundMethod == PaymentMethod.StoreCredit)
            .SumAsync(r => (decimal?)r.RefundedAmount, cancellationToken) ?? 0m;

        // Phase 6 Extension Point:
        // decimal customerPayments = await _context.CustomerPayments.Where(...).SumAsync(...) ?? 0m;
        // decimal balance = openingBalance + salesDue - storeCreditRefunds - customerPayments;

        return openingBalance + salesDue - storeCreditRefunds;
    }

    public async Task<Dictionary<int, decimal>> GetCustomerBalancesBatchAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default)
    {
        var idList = customerIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<int, decimal>();

        var customers = await _context.Customers
            .Where(c => idList.Contains(c.Id))
            .Select(c => new { c.Id, c.OpeningBalance })
            .ToDictionaryAsync(c => c.Id, c => c.OpeningBalance, cancellationToken);

        var salesDueMap = await _context.Sales
            .Where(s => s.CustomerId.HasValue && idList.Contains(s.CustomerId.Value) && s.Status == SaleStatus.Completed)
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Due = g.Sum(s => s.DueAmount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Due, cancellationToken);

        var storeCreditMap = await _context.SaleReturns
            .Where(r => r.CustomerId.HasValue && idList.Contains(r.CustomerId.Value) && r.Status == SaleReturnStatus.Posted && r.RefundMethod == PaymentMethod.StoreCredit)
            .GroupBy(r => r.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Credit = g.Sum(r => r.RefundedAmount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Credit, cancellationToken);

        var result = new Dictionary<int, decimal>();
        foreach (var id in idList)
        {
            decimal opening = customers.TryGetValue(id, out var ob) ? ob : 0m;
            decimal due = salesDueMap.TryGetValue(id, out var d) ? d : 0m;
            decimal credit = storeCreditMap.TryGetValue(id, out var c) ? c : 0m;
            result[id] = opening + due - credit;
        }

        return result;
    }
}

