using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Repositories;

public class SaleRepository : Repository<Sale>, ISaleRepository
{
    public SaleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Sale?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Context.Sales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .Include(s => s.Returns)
                .ThenInclude(r => r.Items)
            .Include(s => s.PriceLevel)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Sale?> GetBySaleNumberWithDetailsAsync(string saleNumber, CancellationToken cancellationToken = default)
    {
        return await Context.Sales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .Include(s => s.Returns)
                .ThenInclude(r => r.Items)
            .Include(s => s.PriceLevel)
            .FirstOrDefaultAsync(s => s.SaleNumber == saleNumber, cancellationToken);
    }

    public async Task<List<Sale>> GetHeldBillsWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Sales
            .Include(s => s.Items)
            .Include(s => s.PriceLevel)
            .Where(s => s.Status == SaleStatus.Held && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<SaleSummaryDto>> SearchSalesAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default)
    {
        var query = Context.Sales
            .AsNoTracking();

        if (criteria.Status.HasValue)
        {
            query = query.Where(s => s.Status == criteria.Status.Value);
        }

        if (criteria.DateFrom.HasValue)
        {
            query = query.Where(s => s.SaleDate >= criteria.DateFrom.Value);
        }

        if (criteria.DateTo.HasValue)
        {
            query = query.Where(s => s.SaleDate <= criteria.DateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(s =>
                (s.SaleNumber != null && s.SaleNumber.ToLower().Contains(term)) ||
                (s.HoldNumber != null && s.HoldNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderByDescending(s => s.SaleDate)
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(s => new
            {
                s.Id,
                s.SaleNumber,
                s.HoldNumber,
                s.SaleDate,
                s.Status,
                s.CustomerName,
                s.CustomerPhone,
                s.GrandTotal,
                s.PaidAmount,
                s.DueAmount,
                s.TotalCost,
                ItemCount = s.Items.Count(),
                PaymentMethods = s.Payments.Select(p => p.Method).ToList()
            })
            .ToListAsync(cancellationToken);

        var items = rawItems.Select(s => new SaleSummaryDto
        {
            Id = s.Id,
            SaleNumber = s.SaleNumber,
            HoldNumber = s.HoldNumber,
            SaleDate = s.SaleDate,
            Status = s.Status,
            CustomerName = s.CustomerName,
            CustomerPhone = s.CustomerPhone,
            GrandTotal = s.GrandTotal,
            PaidAmount = s.PaidAmount,
            DueAmount = s.DueAmount,
            TotalCost = s.TotalCost,
            ItemCount = s.ItemCount,
            PaymentMethodsSummary = string.Join(", ", s.PaymentMethods.Distinct())
        }).ToList();

        return new PagedResult<SaleSummaryDto>(items, totalCount, criteria.PageNumber, criteria.PageSize);
    }

    public async Task<FilteredSalesPaymentBreakdownDto> GetFilteredPaymentBreakdownAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default)
    {
        var query = Context.Sales.AsNoTracking();

        if (criteria.Status.HasValue)
        {
            query = query.Where(s => s.Status == criteria.Status.Value);
        }

        if (criteria.DateFrom.HasValue)
        {
            query = query.Where(s => s.SaleDate >= criteria.DateFrom.Value);
        }

        if (criteria.DateTo.HasValue)
        {
            query = query.Where(s => s.SaleDate <= criteria.DateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(s =>
                (s.SaleNumber != null && s.SaleNumber.ToLower().Contains(term)) ||
                (s.HoldNumber != null && s.HoldNumber.ToLower().Contains(term)));
        }

        var items = await query
            .Select(s => new
            {
                s.Status,
                s.GrandTotal,
                s.DueAmount,
                s.ChangeAmount,
                Payments = s.Payments.Select(p => new { p.Method, p.Amount, p.TenderedAmount })
            })
            .ToListAsync(cancellationToken);

        decimal totalCash = 0m;
        decimal totalCard = 0m;
        decimal totalBank = 0m;
        decimal totalMobileWallet = 0m;
        decimal totalOther = 0m;
        decimal totalGrandTotal = 0m;
        decimal totalDueOld = 0m;
        int count = 0;

        foreach (var s in items)
        {
            if (s.Status == SaleStatus.Completed)
            {
                count++;
                totalGrandTotal += s.GrandTotal;
                totalDueOld += s.DueAmount;

                decimal saleCashPayments = 0m;
                foreach (var p in s.Payments)
                {
                    switch (p.Method)
                    {
                        case PaymentMethod.Cash:
                            saleCashPayments += p.TenderedAmount ?? p.Amount;
                            break;
                        case PaymentMethod.Card:
                            totalCard += p.Amount;
                            break;
                        case PaymentMethod.Bank:
                            totalBank += p.Amount;
                            break;
                        case PaymentMethod.MobileWallet:
                            totalMobileWallet += p.Amount;
                            break;
                        default:
                            totalOther += p.Amount;
                            break;
                    }
                }

                // In cash sales, change is returned in cash from cash received
                totalCash += Math.Max(0m, saleCashPayments - s.ChangeAmount);
            }
        }

        return new FilteredSalesPaymentBreakdownDto
        {
            SalesCount = count,
            TotalGrandTotal = totalGrandTotal,
            TotalCash = totalCash,
            TotalCard = totalCard,
            TotalBank = totalBank,
            TotalMobileWallet = totalMobileWallet,
            TotalOther = totalOther,
            TotalDueOld = totalDueOld
        };
    }

    public async Task<IReadOnlyList<int>> GetFilteredIdsAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default)
    {
        var query = Context.Sales.AsNoTracking();

        if (criteria.Status.HasValue)
            query = query.Where(s => s.Status == criteria.Status.Value);

        if (criteria.DateFrom.HasValue)
            query = query.Where(s => s.SaleDate >= criteria.DateFrom.Value);

        if (criteria.DateTo.HasValue)
            query = query.Where(s => s.SaleDate <= criteria.DateTo.Value);

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(s =>
                s.SaleNumber.ToLower().Contains(term) ||
                (s.Notes != null && s.Notes.ToLower().Contains(term)));
        }

        return await query.Select(s => s.Id).ToListAsync(cancellationToken);
    }

    public async Task<TodaySalesSummaryDto> GetTodaySummaryAsync(DateTime startOfDayUtc, DateTime endOfDayUtc, CancellationToken cancellationToken = default)
    {
        var sales = await Context.Sales
            .Include(s => s.Payments)
            .Where(s => s.SaleDate >= startOfDayUtc && s.SaleDate <= endOfDayUtc && s.Status == SaleStatus.Completed && !s.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var returns = await Context.SaleReturns
            .Include(r => r.Items)
            .Where(r => r.ReturnDate >= startOfDayUtc && r.ReturnDate <= endOfDayUtc && r.Status == SaleReturnStatus.Posted && !r.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var summary = new TodaySalesSummaryDto
        {
            SalesCount = sales.Count,
            GrossSales = sales.Sum(s => s.SubTotal),
            TotalDiscounts = sales.Sum(s => s.LineDiscountTotal + s.BillDiscountAmount),
            TotalTax = sales.Sum(s => s.TaxAmount),
            NetSales = sales.Sum(s => s.GrandTotal),
            TotalCost = sales.Sum(s => s.TotalCost),
            TotalReturnsCost = returns.SelectMany(r => r.Items).Sum(i => i.CostPriceSnapshot * i.Quantity),
            Profit = sales.Sum(s => s.GrandTotal - s.TotalCost)
        };

        foreach (var sale in sales)
        {
            decimal saleCash = 0m;
            foreach (var payment in sale.Payments)
            {
                switch (payment.Method)
                {
                    case PaymentMethod.Cash:
                        saleCash += payment.Amount;
                        break;
                    case PaymentMethod.Card:
                        summary.CardSales += payment.Amount;
                        break;
                    case PaymentMethod.Bank:
                        summary.BankSales += payment.Amount;
                        break;
                    default:
                        summary.OtherSales += payment.Amount;
                        break;
                }
            }
            summary.CashSales += Math.Max(0m, saleCash - sale.ChangeAmount);
        }

        return summary;
    }
}
