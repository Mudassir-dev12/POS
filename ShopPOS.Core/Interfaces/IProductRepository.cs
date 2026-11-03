using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<PagedResult<ProductListItemDto>> GetPagedAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
    Task<Product?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetByIdsWithDetailsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    Task<Product?> FindByBarcodeOrSkuWithDetailsAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> IsSkuExistsAsync(string sku, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<ProductBarcodeConflict?> FindBarcodeConflictAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default);
    Task<string> GenerateNextSkuAsync(CancellationToken cancellationToken = default);
    Task<int> GetReferenceCountAsync(int productId, CancellationToken cancellationToken = default);
    Task<Product> CreateWithOpeningStockAsync(Product product, decimal openingQuantity, decimal unitCost, int? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductPriceHistoryDto>> GetPriceHistoriesAsync(int productId, CancellationToken cancellationToken = default);
    Task SyncProductBarcodesAsync(int productId, string? primaryBarcode, IEnumerable<string> additionalBarcodes, CancellationToken cancellationToken = default);
    Task SyncProductPricesAsync(int productId, IEnumerable<ProductPriceDto> prices, CancellationToken cancellationToken = default);
    Task RecordPriceChangeAsync(int productId, decimal oldCost, decimal newCost, decimal oldSale, decimal newSale, int? userId, CancellationToken cancellationToken = default);
    Task RecordStockAdjustmentAsync(int productId, decimal quantityDelta, decimal unitCost, string? note, int? userId, CancellationToken cancellationToken = default);
    Task<decimal> GetCurrentStockAsync(int productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetAllForExportAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetFilteredIdsAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
}
