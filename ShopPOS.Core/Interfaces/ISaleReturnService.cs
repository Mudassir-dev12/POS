using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface ISaleReturnService
{
    Task<Result<List<ReturnableLineDto>>> LoadReturnableLinesAsync(int saleId, CancellationToken cancellationToken = default);
    Task<Result<SaleReturnDetailDto>> CreateSaleReturnAsync(CreateSaleReturnDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> CancelSaleReturnAsync(int returnId, string reason, CancellationToken cancellationToken = default);
    Task<Result<SaleReturnDetailDto>> GetSaleReturnByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<SaleReturnDetailDto>> SearchSaleReturnsAsync(SaleReturnSearchDto criteria, CancellationToken cancellationToken = default);
    Task<BulkOperationResult> BulkCancelSaleReturnsAsync(IEnumerable<int> ids, string reason, int? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetFilteredSaleReturnIdsAsync(SaleReturnSearchDto criteria, CancellationToken cancellationToken = default);
}

