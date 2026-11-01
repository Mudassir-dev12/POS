using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface ISaleService
{
    Task<Result<SaleDetailDto>> SaveHeldBillAsync(SaveSaleDto dto, CancellationToken cancellationToken = default);
    Task<Result<List<SaleDetailDto>>> ListHeldBillsAsync(CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteHeldBillAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<SaleDetailDto>> CompleteSaleAsync(CompleteSaleDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> CancelSaleAsync(int saleId, string reason, CancellationToken cancellationToken = default);
    Task<Result<SaleDetailDto>> GetSaleByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<SaleSummaryDto>> SearchSalesAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default);
    Task<Result<TodaySalesSummaryDto>> GetTodaySummaryAsync(CancellationToken cancellationToken = default);
}

