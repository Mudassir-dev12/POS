using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Repositories;

public class SupplierPaymentRepository : ISupplierPaymentRepository
{
    private readonly AppDbContext _context;

    public SupplierPaymentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<SupplierPaymentDto>> GetPaymentsAsync(int? supplierId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.SupplierPayments
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (supplierId.HasValue && supplierId.Value > 0)
        {
            dbQuery = dbQuery.Where(p => p.SupplierId == supplierId.Value);
        }

        int totalCount = await dbQuery.CountAsync(cancellationToken);
        int page = Math.Max(1, pageNumber);
        int size = Math.Max(1, pageSize);

        var items = await dbQuery
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new SupplierPaymentDto
            {
                Id = p.Id,
                PaymentNumber = p.PaymentNumber,
                SupplierId = p.SupplierId,
                SupplierName = p.Supplier != null ? p.Supplier.Name : string.Empty,
                PurchaseId = p.PurchaseId,
                PurchaseNumber = p.Purchase != null ? p.Purchase.PurchaseNumber : null,
                PaymentDate = p.PaymentDate,
                Amount = p.Amount,
                Method = p.Method,
                Reference = p.Reference,
                Notes = p.Notes,
                IsCancelled = p.IsCancelled,
                CancelReason = p.CancelReason,
                CancelledAt = p.CancelledAt,
                CancelledByName = p.CancelledByUser != null ? p.CancelledByUser.FullName : null
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierPaymentDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = size
        };
    }

    public async Task<IReadOnlyList<SupplierPaymentDto>> GetPaymentsByPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        return await _context.SupplierPayments
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.PurchaseId == purchaseId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new SupplierPaymentDto
            {
                Id = p.Id,
                PaymentNumber = p.PaymentNumber,
                SupplierId = p.SupplierId,
                SupplierName = p.Supplier != null ? p.Supplier.Name : string.Empty,
                PurchaseId = p.PurchaseId,
                PurchaseNumber = p.Purchase != null ? p.Purchase.PurchaseNumber : null,
                PaymentDate = p.PaymentDate,
                Amount = p.Amount,
                Method = p.Method,
                Reference = p.Reference,
                Notes = p.Notes,
                IsCancelled = p.IsCancelled,
                CancelReason = p.CancelReason,
                CancelledAt = p.CancelledAt,
                CancelledByName = p.CancelledByUser != null ? p.CancelledByUser.FullName : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierPayment?> GetPaymentByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.SupplierPayments
            .Include(p => p.Supplier)
            .Include(p => p.Purchase)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public async Task<SupplierPayment> CreatePaymentAsync(SupplierPayment payment, CancellationToken cancellationToken = default)
    {
        await _context.SupplierPayments.AddAsync(payment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return payment;
    }

    public async Task CancelPaymentAsync(int paymentId, string reason, int userId, CancellationToken cancellationToken = default)
    {
        var payment = await _context.SupplierPayments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment != null)
        {
            payment.IsCancelled = true;
            payment.CancelReason = reason;
            payment.CancelledAt = DateTime.UtcNow;
            payment.CancelledByUserId = userId;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<decimal> GetTotalPaidForPurchaseAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        return await _context.SupplierPayments
            .Where(p => !p.IsDeleted && !p.IsCancelled && p.PurchaseId == purchaseId)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
    }

    public async Task<IReadOnlyList<int>> GetFilteredIdsAsync(int? supplierId, CancellationToken cancellationToken = default)
    {
        var query = _context.SupplierPayments.AsNoTracking().Where(p => !p.IsDeleted);
        if (supplierId.HasValue && supplierId.Value > 0)
            query = query.Where(p => p.SupplierId == supplierId.Value);
        return await query.Select(p => p.Id).ToListAsync(cancellationToken);
    }

    public async Task UpdatePurchasePaidAmountAsync(int purchaseId, decimal newPaidAmount, CancellationToken cancellationToken = default)
    {
        var purchase = await _context.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId, cancellationToken);
        if (purchase != null)
        {
            purchase.PaidAmount = newPaidAmount;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
