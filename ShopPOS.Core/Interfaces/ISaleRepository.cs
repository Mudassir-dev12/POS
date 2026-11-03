using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface ISaleRepository : IRepository<Sale>
{
    Task<Sale?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<Sale?> GetBySaleNumberWithDetailsAsync(string saleNumber, CancellationToken cancellationToken = default);
    Task<List<Sale>> GetHeldBillsWithDetailsAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<SaleSummaryDto>> SearchSalesAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default);
    Task<FilteredSalesPaymentBreakdownDto> GetFilteredPaymentBreakdownAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default);
    Task<TodaySalesSummaryDto> GetTodaySummaryAsync(DateTime startOfDayUtc, DateTime endOfDayUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetFilteredIdsAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default);
}
