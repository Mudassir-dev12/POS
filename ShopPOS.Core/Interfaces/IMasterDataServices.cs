using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IProductService
{
    Task<Result<PagedResult<ProductListItemDto>>> GetPagedProductsAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> CreateProductAsync(CreateProductDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> UpdateProductAsync(UpdateProductDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeactivateProductAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeleteProductAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> DuplicateProductAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProductPriceHistoryDto>>> GetPriceHistoryAsync(int productId, CancellationToken cancellationToken = default);
    Task<Result<string>> GenerateNextSkuAsync(CancellationToken cancellationToken = default);
    Task<Result<string?>> ValidateBarcodeAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default);
}

public interface ICategoryService
{
    Task<Result<IReadOnlyList<CategoryDto>>> GetCategoryTreeAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CategoryDto>>> GetFlatListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> UpdateCategoryAsync(UpdateCategoryDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeactivateCategoryAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeleteCategoryAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
}

public interface IBrandService
{
    Task<Result<PagedResult<BrandDto>>> GetPagedBrandsAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BrandDto>>> GetFlatListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> CreateBrandAsync(CreateBrandDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> UpdateBrandAsync(UpdateBrandDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeactivateBrandAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeleteBrandAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
}

public interface IUnitService
{
    Task<Result<PagedResult<UnitDto>>> GetPagedUnitsAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<UnitDto>>> GetFlatListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Result<UnitDto>> CreateUnitAsync(CreateUnitDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<UnitDto>> UpdateUnitAsync(UpdateUnitDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeactivateUnitAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeleteUnitAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
}

public interface ISupplierService
{
    Task<Result<PagedResult<SupplierDto>>> GetPagedSuppliersAsync(MasterDataSearchQuery query, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SupplierDto>>> GetFlatListAsync(bool includeInactive = false, string? userRole = null, CancellationToken cancellationToken = default);
    Task<Result<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<SupplierDto>> UpdateSupplierAsync(UpdateSupplierDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeactivateSupplierAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeleteSupplierAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
}

public interface ICustomerService
{
    Task<Result<PagedResult<CustomerDto>>> GetPagedCustomersAsync(MasterDataSearchQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CustomerDto>>> GetFlatListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Result<CustomerDto>> CreateCustomerAsync(CreateCustomerDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result<CustomerDto>> UpdateCustomerAsync(UpdateCustomerDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeactivateCustomerAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
    Task<Result> DeleteCustomerAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default);
}

public interface IPriceLevelService
{
    Task<Result<IReadOnlyList<PriceLevelDto>>> GetAllAsync(CancellationToken cancellationToken = default);
}

