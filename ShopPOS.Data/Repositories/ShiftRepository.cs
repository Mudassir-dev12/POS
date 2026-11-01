using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Repositories;

public class ShiftRepository : Repository<Shift>, IShiftRepository
{
    public ShiftRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Shift?> GetOpenShiftAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Shifts
            .Include(s => s.CashMovements)
            .FirstOrDefaultAsync(s => s.Status == ShiftStatus.Open, cancellationToken);
    }

    public async Task<Shift?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Context.Shifts
            .Include(s => s.CashMovements)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<ShiftSummaryDto> GetShiftSummaryAsync(int id, CancellationToken cancellationToken = default)
    {
        var shift = await Context.Shifts
            .Include(s => s.CashMovements)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (shift == null)
        {
            throw new KeyNotFoundException($"Shift with ID {id} was not found.");
        }

        var sales = await Context.Sales
            .Include(s => s.Payments)
            .Where(s => s.ShiftId == id && s.Status == SaleStatus.Completed)
            .ToListAsync(cancellationToken);

        var returns = await Context.SaleReturns
            .Where(r => r.ShiftId == id && r.Status == SaleReturnStatus.Posted)
            .ToListAsync(cancellationToken);

        var heldBillsCount = await Context.Sales
            .CountAsync(s => s.Status == SaleStatus.Held, cancellationToken);

        var paymentsByMethod = new Dictionary<PaymentMethod, decimal>();
        foreach (var method in Enum.GetValues<PaymentMethod>())
        {
            paymentsByMethod[method] = 0m;
        }

        decimal totalCashSales = 0m;
        foreach (var sale in sales)
        {
            foreach (var p in sale.Payments)
            {
                paymentsByMethod[p.Method] += p.Amount;
                if (p.Method == PaymentMethod.Cash)
                {
                    totalCashSales += p.Amount;
                }
            }
        }

        decimal cashRefunds = returns
            .Where(r => r.RefundMethod == PaymentMethod.Cash)
            .Sum(r => r.RefundedAmount);

        decimal cashIn = shift.CashMovements
            .Where(m => m.Type == CashMovementType.CashIn)
            .Sum(m => m.Amount);

        decimal cashOut = shift.CashMovements
            .Where(m => m.Type == CashMovementType.CashOut)
            .Sum(m => m.Amount);

        // Expected Cash = OpeningCash + Net Cash Sales - Cash Refunds + CashIn - CashOut
        decimal expectedCash = Math.Round(shift.OpeningCash + totalCashSales - cashRefunds + cashIn - cashOut, 2, MidpointRounding.AwayFromZero);

        decimal? diff = null;
        if (shift.Status == ShiftStatus.Closed && shift.CountedCash.HasValue)
        {
            diff = shift.Difference ?? Math.Round(shift.CountedCash.Value - expectedCash, 2, MidpointRounding.AwayFromZero);
        }

        string? warning = null;
        if (heldBillsCount > 0)
        {
            warning = $"There are {heldBillsCount} active held bill(s) in the system.";
        }

        return new ShiftSummaryDto
        {
            ShiftId = shift.Id,
            ShiftNumber = shift.ShiftNumber,
            Status = shift.Status,
            OpenedAt = shift.OpenedAt,
            ClosedAt = shift.ClosedAt,
            OpeningCash = shift.OpeningCash,
            SalesCount = sales.Count,
            GrossSales = sales.Sum(s => s.SubTotal),
            TotalDiscounts = sales.Sum(s => s.LineDiscountTotal + s.BillDiscountAmount),
            TotalTax = sales.Sum(s => s.TaxAmount),
            NetSales = sales.Sum(s => s.GrandTotal),
            ReturnsCount = returns.Count,
            TotalReturns = returns.Sum(r => r.TotalAmount),
            PaymentsByMethod = paymentsByMethod,
            CashIn = cashIn,
            CashOut = cashOut,
            ExpectedCash = expectedCash,
            CountedCash = shift.CountedCash,
            Difference = diff,
            HeldBillsCount = heldBillsCount,
            WarningMessage = warning
        };
    }

    public async Task<PagedResult<Shift>> SearchShiftsAsync(ShiftSearchDto criteria, CancellationToken cancellationToken = default)
    {
        var query = Context.Shifts.AsNoTracking();

        if (criteria.Status.HasValue)
        {
            query = query.Where(s => s.Status == criteria.Status.Value);
        }

        if (criteria.DateFrom.HasValue)
        {
            query = query.Where(s => s.OpenedAt >= criteria.DateFrom.Value);
        }

        if (criteria.DateTo.HasValue)
        {
            query = query.Where(s => s.OpenedAt <= criteria.DateTo.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.OpenedAt)
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Shift>(items, total, criteria.PageNumber, criteria.PageSize);
    }

    public async Task<CashMovement> AddCashMovementAsync(CashMovement movement, CancellationToken cancellationToken = default)
    {
        await Context.CashMovements.AddAsync(movement, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return movement;
    }
}
