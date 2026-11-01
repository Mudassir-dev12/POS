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
            .Include(s => s.Items.OrderBy(i => i.LineNo))
            .Include(s => s.Payments)
            .Include(s => s.Returns)
                .ThenInclude(r => r.Items)
            .Include(s => s.Customer)
            .Include(s => s.PriceLevel)
            .Include(s => s.Shift)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Sale?> GetBySaleNumberWithDetailsAsync(string saleNumber, CancellationToken cancellationToken = default)
    {
        return await Context.Sales
            .Include(s => s.Items.OrderBy(i => i.LineNo))
            .Include(s => s.Payments)
            .Include(s => s.Returns)
            .Include(s => s.Customer)
            .Include(s => s.PriceLevel)
            .Include(s => s.Shift)
            .FirstOrDefaultAsync(s => s.SaleNumber == saleNumber, cancellationToken);
    }

    public async Task<List<Sale>> GetHeldBillsWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Sales
            .Include(s => s.Items.OrderBy(i => i.LineNo))
            .Include(s => s.Customer)
            .Include(s => s.PriceLevel)
            .Where(s => s.Status == SaleStatus.Held)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<SaleSummaryDto>> SearchSalesAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default)
    {
        var query = Context.Sales
            .Include(s => s.Customer)
            .Include(s => s.Shift)
            .AsNoTracking();

        if (criteria.Status.HasValue)
        {
            query = query.Where(s => s.Status == criteria.Status.Value);
        }

        if (criteria.CustomerId.HasValue)
        {
            query = query.Where(s => s.CustomerId == criteria.CustomerId.Value);
        }

        if (criteria.ShiftId.HasValue)
        {
            query = query.Where(s => s.ShiftId == criteria.ShiftId.Value);
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
                (s.HoldNumber != null && s.HoldNumber.ToLower().Contains(term)) ||
                (s.Customer != null && s.Customer.Name.ToLower().Contains(term)));
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
                s.CustomerId,
                CustomerName = s.Customer != null ? s.Customer.Name : "Walk-in",
                s.Status,
                s.GrandTotal,
                s.PaidAmount,
                s.DueAmount,
                s.TotalCost,
                ShiftNumber = s.Shift != null ? s.Shift.ShiftNumber : null,
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
            CustomerId = s.CustomerId,
            CustomerName = s.CustomerName,
            Status = s.Status,
            GrandTotal = s.GrandTotal,
            PaidAmount = s.PaidAmount,
            DueAmount = s.DueAmount,
            TotalCost = s.TotalCost,
            ShiftNumber = s.ShiftNumber,
            ItemCount = s.ItemCount,
            PaymentMethodsSummary = string.Join(", ", s.PaymentMethods.Distinct())
        }).ToList();

        return new PagedResult<SaleSummaryDto>(items, totalCount, criteria.PageNumber, criteria.PageSize);
    }

    public async Task<TodaySalesSummaryDto> GetTodaySummaryAsync(DateTime startOfDayUtc, DateTime endOfDayUtc, CancellationToken cancellationToken = default)
    {
        var sales = await Context.Sales
            .Include(s => s.Payments)
            .Where(s => s.Status == SaleStatus.Completed && s.SaleDate >= startOfDayUtc && s.SaleDate <= endOfDayUtc)
            .ToListAsync(cancellationToken);

        var returns = await Context.SaleReturns
            .Include(r => r.Items)
            .Where(r => r.Status == SaleReturnStatus.Posted && r.ReturnDate >= startOfDayUtc && r.ReturnDate <= endOfDayUtc)
            .ToListAsync(cancellationToken);

        decimal totalReturnsCost = 0m;
        foreach (var r in returns)
        {
            foreach (var item in r.Items)
            {
                totalReturnsCost += item.CostPriceSnapshot * item.Quantity;
            }
        }

        var result = new TodaySalesSummaryDto
        {
            SalesCount = sales.Count,
            GrossSales = sales.Sum(s => s.SubTotal),
            TotalDiscounts = sales.Sum(s => s.LineDiscountTotal + s.BillDiscountAmount),
            TotalTax = sales.Sum(s => s.TaxAmount),
            NetSales = sales.Sum(s => s.GrandTotal),
            TotalCost = sales.Sum(s => s.TotalCost),
            TotalReturnsCost = Math.Round(totalReturnsCost, 2, MidpointRounding.AwayFromZero)
        };

        decimal totalNetOfTax = sales.Sum(s => s.GrandTotal - s.TaxAmount);
        result.Profit = Math.Round(totalNetOfTax - result.TotalCost - result.TotalReturnsCost, 2, MidpointRounding.AwayFromZero);

        foreach (var sale in sales)
        {
            foreach (var pay in sale.Payments)
            {
                switch (pay.Method)
                {
                    case PaymentMethod.Cash:
                        result.CashSales += pay.Amount;
                        break;
                    case PaymentMethod.Card:
                        result.CardSales += pay.Amount;
                        break;
                    case PaymentMethod.Bank:
                        result.BankSales += pay.Amount;
                        break;
                    default:
                        result.OtherSales += pay.Amount;
                        break;
                }
            }
        }

        return result;
    }

    public async Task<bool> HasSalesInShiftAsync(int shiftId, CancellationToken cancellationToken = default)
    {
        return await Context.Sales.AnyAsync(s => s.ShiftId == shiftId && s.Status == SaleStatus.Completed, cancellationToken);
    }
}
