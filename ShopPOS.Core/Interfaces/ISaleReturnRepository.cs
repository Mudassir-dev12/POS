using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface ISaleReturnRepository : IRepository<SaleReturn>
{
    Task<SaleReturn?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<List<SaleReturn>> GetReturnsForSaleAsync(int saleId, CancellationToken cancellationToken = default);
    Task<PagedResult<SaleReturnDetailDto>> SearchSaleReturnsAsync(SaleReturnSearchDto criteria, CancellationToken cancellationToken = default);
}

