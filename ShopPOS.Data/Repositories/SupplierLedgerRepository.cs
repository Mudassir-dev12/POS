using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Repositories;

public class SupplierLedgerRepository : ISupplierLedgerRepository
{
    private readonly AppDbContext _context;

    public SupplierLedgerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetSupplierBalanceAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);

        if (supplier == null) return 0m;

        decimal opening = supplier.OpeningBalance;

        decimal purchasesTotal = await _context.Purchases
            .Where(p => !p.IsDeleted && p.SupplierId == supplierId && p.Status == PurchaseStatus.Posted)
            .SumAsync(p => (decimal?)p.GrandTotal, cancellationToken) ?? 0m;

        decimal returnsTotal = await _context.PurchaseReturns
            .Where(r => !r.IsDeleted && r.SupplierId == supplierId && r.Status == PurchaseReturnStatus.Posted)
            .SumAsync(r => (decimal?)r.TotalAmount, cancellationToken) ?? 0m;

        decimal paymentsTotal = await _context.SupplierPayments
            .Where(p => !p.IsDeleted && p.SupplierId == supplierId && !p.IsCancelled)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        return opening + purchasesTotal - returnsTotal - paymentsTotal;
    }

    public async Task<decimal> GetTotalPayableBalanceAsync(CancellationToken cancellationToken = default)
    {
        decimal opening = await _context.Suppliers
            .Where(s => !s.IsDeleted)
            .SumAsync(s => (decimal?)s.OpeningBalance, cancellationToken) ?? 0m;

        decimal purchasesTotal = await _context.Purchases
            .Where(p => !p.IsDeleted && p.Status == PurchaseStatus.Posted)
            .SumAsync(p => (decimal?)p.GrandTotal, cancellationToken) ?? 0m;

        decimal returnsTotal = await _context.PurchaseReturns
            .Where(r => !r.IsDeleted && r.Status == PurchaseReturnStatus.Posted)
            .SumAsync(r => (decimal?)r.TotalAmount, cancellationToken) ?? 0m;

        decimal paymentsTotal = await _context.SupplierPayments
            .Where(p => !p.IsDeleted && !p.IsCancelled)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        return opening + purchasesTotal - returnsTotal - paymentsTotal;
    }

    public async Task<Dictionary<int, decimal>> GetSupplierBalancesAsync(IEnumerable<int> supplierIds, CancellationToken cancellationToken = default)
    {
        var idList = supplierIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<int, decimal>();

        var suppliers = await _context.Suppliers
            .AsNoTracking()
            .Where(s => idList.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.OpeningBalance, cancellationToken);

        var purchases = await _context.Purchases
            .AsNoTracking()
            .Where(p => !p.IsDeleted && idList.Contains(p.SupplierId) && p.Status == PurchaseStatus.Posted)
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(x => x.GrandTotal) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total, cancellationToken);

        var returns = await _context.PurchaseReturns
            .AsNoTracking()
            .Where(r => !r.IsDeleted && idList.Contains(r.SupplierId) && r.Status == PurchaseReturnStatus.Posted)
            .GroupBy(r => r.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total, cancellationToken);

        var payments = await _context.SupplierPayments
            .AsNoTracking()
            .Where(p => !p.IsDeleted && idList.Contains(p.SupplierId) && !p.IsCancelled)
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total, cancellationToken);

        var result = new Dictionary<int, decimal>();
        foreach (var id in idList)
        {
            decimal opening = suppliers.TryGetValue(id, out var op) ? op : 0m;
            decimal pur = purchases.TryGetValue(id, out var p) ? p : 0m;
            decimal ret = returns.TryGetValue(id, out var r) ? r : 0m;
            decimal pay = payments.TryGetValue(id, out var m) ? m : 0m;

            result[id] = opening + pur - ret - pay;
        }

        return result;
    }

    public async Task<SupplierLedgerReportDto> GetSupplierLedgerReportAsync(int supplierId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);

        string supplierName = supplier?.Name ?? $"Supplier #{supplierId}";
        decimal initialOpening = supplier?.OpeningBalance ?? 0m;

        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1).AddTicks(-1);

        // Calculate opening balance before 'from'
        decimal prePurchases = await _context.Purchases
            .Where(p => !p.IsDeleted && p.SupplierId == supplierId && p.Status == PurchaseStatus.Posted && p.PurchaseDate < from)
            .SumAsync(p => (decimal?)p.GrandTotal, cancellationToken) ?? 0m;

        decimal preReturns = await _context.PurchaseReturns
            .Where(r => !r.IsDeleted && r.SupplierId == supplierId && r.Status == PurchaseReturnStatus.Posted && r.ReturnDate < from)
            .SumAsync(r => (decimal?)r.TotalAmount, cancellationToken) ?? 0m;

        decimal prePayments = await _context.SupplierPayments
            .Where(p => !p.IsDeleted && p.SupplierId == supplierId && !p.IsCancelled && p.PaymentDate < from)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        decimal openingBalance = initialOpening + prePurchases - preReturns - prePayments;

        // Fetch period entries
        var periodPurchases = await _context.Purchases
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.SupplierId == supplierId && p.Status == PurchaseStatus.Posted && p.PurchaseDate >= from && p.PurchaseDate <= to)
            .Select(p => new
            {
                Date = p.PurchaseDate,
                Type = "Purchase",
                Reference = string.IsNullOrWhiteSpace(p.SupplierInvoiceNo)
                    ? p.PurchaseNumber
                    : $"{p.PurchaseNumber} (Inv: {p.SupplierInvoiceNo})",
                p.Notes,
                Debit = p.GrandTotal,
                Credit = 0m
            })
            .ToListAsync(cancellationToken);

        var periodReturns = await _context.PurchaseReturns
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.SupplierId == supplierId && r.Status == PurchaseReturnStatus.Posted && r.ReturnDate >= from && r.ReturnDate <= to)
            .Select(r => new
            {
                Date = r.ReturnDate,
                Type = "Purchase Return",
                Reference = r.ReturnNumber,
                r.Notes,
                Debit = 0m,
                Credit = r.TotalAmount
            })
            .ToListAsync(cancellationToken);

        var periodPayments = await _context.SupplierPayments
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.SupplierId == supplierId && !p.IsCancelled && p.PaymentDate >= from && p.PaymentDate <= to)
            .Select(p => new
            {
                Date = p.PaymentDate,
                Type = "Payment",
                Reference = string.IsNullOrWhiteSpace(p.Reference)
                    ? p.PaymentNumber
                    : $"{p.PaymentNumber} ({p.Reference})",
                p.Notes,
                Debit = 0m,
                Credit = p.Amount
            })
            .ToListAsync(cancellationToken);

        // Combine and sort chronologically
        var rawEntries = periodPurchases
            .Concat(periodReturns)
            .Concat(periodPayments)
            .OrderBy(e => e.Date)
            .ToList();

        decimal running = openingBalance;
        decimal totalDebits = 0m;
        decimal totalCredits = 0m;

        var entries = new List<SupplierLedgerEntryDto>();
        foreach (var item in rawEntries)
        {
            running += (item.Debit - item.Credit);
            totalDebits += item.Debit;
            totalCredits += item.Credit;

            entries.Add(new SupplierLedgerEntryDto
            {
                Date = item.Date,
                Type = item.Type,
                Reference = item.Reference,
                Notes = item.Notes,
                Debit = item.Debit,
                Credit = item.Credit,
                RunningBalance = running
            });
        }

        return new SupplierLedgerReportDto
        {
            SupplierId = supplierId,
            SupplierName = supplierName,
            FromDate = fromDate,
            ToDate = toDate,
            OpeningBalance = openingBalance,
            TotalDebits = totalDebits,
            TotalCredits = totalCredits,
            ClosingBalance = running,
            Entries = entries
        };
    }
}

