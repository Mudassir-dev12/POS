using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface ICategoryRepository : IRepository<Category>
{
    Task<IReadOnlyList<CategoryDto>> GetCategoryTreeAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetAllFlatAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<bool> HasProductsOrChildrenAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<bool> WouldCreateCycleAsync(int categoryId, int? newParentId, CancellationToken cancellationToken = default);
    Task<bool> IsNameTakenUnderParentAsync(string name, int? parentId, int? excludeId = null, CancellationToken cancellationToken = default);
}

public interface IBrandRepository : IRepository<Brand>
{
    Task<PagedResult<BrandDto>> GetPagedAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Brand>> GetAllFlatAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<bool> HasProductsAsync(int brandId, CancellationToken cancellationToken = default);
    Task<bool> IsNameTakenAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
}

public interface IUnitRepository : IRepository<Unit>
{
    Task<PagedResult<UnitDto>> GetPagedAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Unit>> GetAllFlatAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<bool> HasProductsAsync(int unitId, CancellationToken cancellationToken = default);
    Task<bool> IsNameTakenAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
}

public interface ISupplierRepository : IRepository<Supplier>
{
    Task<PagedResult<SupplierDto>> GetPagedAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Supplier>> GetAllFlatAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<bool> HasTransactionsAsync(int supplierId, CancellationToken cancellationToken = default);
    Task<bool> IsNamePhoneTakenAsync(string name, string? phone, int? excludeId = null, CancellationToken cancellationToken = default);
}

public interface ICustomerRepository : IRepository<Customer>
{
    Task<PagedResult<CustomerDto>> GetPagedAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Customer>> GetAllFlatAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<bool> HasTransactionsAsync(int customerId, CancellationToken cancellationToken = default);
    Task<bool> IsPhoneTakenAsync(string phone, int? excludeId = null, CancellationToken cancellationToken = default);
}

public interface IPriceLevelRepository : IRepository<PriceLevel>
{
    Task<IReadOnlyList<PriceLevel>> GetAllActiveAsync(CancellationToken cancellationToken = default);
}

