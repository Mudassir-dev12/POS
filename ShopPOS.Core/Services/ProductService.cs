using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Validators;

namespace ShopPOS.Core.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IShopSettingRepository _shopSettingRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly ITransactionRunner? _transactionRunner;
    private readonly IStockAdjustmentService? _stockAdjustmentService;
    private readonly CreateProductValidator _createValidator = new();
    private readonly UpdateProductValidator _updateValidator = new();

    public ProductService(
        IProductRepository productRepository,
        IShopSettingRepository shopSettingRepository,
        IAuditLogService auditLogService,
        ITransactionRunner? transactionRunner = null,
        IStockAdjustmentService? stockAdjustmentService = null)
    {
        _productRepository = productRepository;
        _shopSettingRepository = shopSettingRepository;
        _auditLogService = auditLogService;
        _transactionRunner = transactionRunner;
        _stockAdjustmentService = stockAdjustmentService;
    }

    private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (_transactionRunner != null)
        {
            return await _transactionRunner.RunInTransactionAsync(action, cancellationToken);
        }

        return await action();
    }

    public async Task<Result<PagedResult<ProductListItemDto>>> GetPagedProductsAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var paged = await _productRepository.GetPagedAsync(query, cancellationToken);
            return Result.Ok(paged);
        }
        catch (Exception ex)
        {
            return Result.Failure<PagedResult<ProductListItemDto>>($"Error fetching products: {ex.Message}");
        }
    }

    public async Task<Result<ProductDto>> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetWithDetailsAsync(id, cancellationToken);
        if (product == null)
        {
            return Result.Failure<ProductDto>($"Product with ID {id} was not found.");
        }

        decimal currentStock = await _productRepository.GetCurrentStockAsync(id, cancellationToken);

        var dto = new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Barcode = product.Barcode,
            Name = product.Name,
            Description = product.Description,
            ImagePath = product.ImagePath,
            CategoryId = product.CategoryId,
            CategoryName = product.Category != null ? product.Category.Name : string.Empty,
            BrandId = product.BrandId,
            BrandName = product.Brand != null ? product.Brand.Name : null,
            UnitId = product.UnitId,
            UnitName = product.Unit != null ? product.Unit.Name : string.Empty,
            UnitShortName = product.Unit != null ? product.Unit.ShortName : string.Empty,
            CostPrice = product.CostPrice,
            SalePrice = product.SalePrice,
            MinSalePrice = product.MinSalePrice,
            TaxPercent = product.TaxPercent,
            ReorderLevel = product.ReorderLevel,
            CurrentStock = currentStock,
            TrackStock = product.TrackStock,
            IsWeighable = product.IsWeighable,
            AllowDecimalQuantity = product.AllowDecimalQuantity,
            IsActive = product.IsActive,
            AdditionalBarcodes = product.AdditionalBarcodes
                .Where(ab => !ab.IsPrimary)
                .Select(ab => new ProductBarcodeDto
                {
                    Id = ab.Id,
                    Barcode = ab.Barcode,
                    IsPrimary = ab.IsPrimary
                }).ToList(),
            ProductPrices = product.ProductPrices
                .Select(pp => new ProductPriceDto
                {
                    Id = pp.Id,
                    PriceLevelId = pp.PriceLevelId,
                    PriceLevelName = pp.PriceLevel != null ? pp.PriceLevel.Name : string.Empty,
                    Price = pp.Price
                }).ToList()
        };

        return Result.Ok(dto);
    }

    public async Task<Result<ProductDto>> CreateProductAsync(CreateProductDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var val = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<ProductDto>(val.ToDictionary(), "Validation failed for product.");
        }

        // 1. SKU generation or validation
        string sku = string.IsNullOrWhiteSpace(dto.Sku)
            ? await _productRepository.GenerateNextSkuAsync(cancellationToken)
            : dto.Sku.Trim();

        if (await _productRepository.IsSkuExistsAsync(sku, null, cancellationToken))
        {
            return Result.Failure<ProductDto>($"SKU '{sku}' is already assigned to another product.");
        }

        // Promote first additional barcode to primary if primary barcode was left empty
        if (string.IsNullOrWhiteSpace(dto.Barcode) && dto.AdditionalBarcodes.Count > 0)
        {
            var firstValid = dto.AdditionalBarcodes.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
            if (firstValid != null)
            {
                dto.Barcode = firstValid.Trim();
                dto.AdditionalBarcodes.Remove(firstValid);
            }
        }

        // Promote first additional barcode to primary if primary barcode was left empty
        if (string.IsNullOrWhiteSpace(dto.Barcode) && dto.AdditionalBarcodes.Count > 0)
        {
            var firstValid = dto.AdditionalBarcodes.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
            if (firstValid != null)
            {
                dto.Barcode = firstValid.Trim();
                dto.AdditionalBarcodes.Remove(firstValid);
            }
        }

        // 2. Barcode conflict validation
        if (!string.IsNullOrWhiteSpace(dto.Barcode))
        {
            var conflict = await _productRepository.FindBarcodeConflictAsync(dto.Barcode.Trim(), null, cancellationToken);
            if (conflict != null)
            {
                return Result.Failure<ProductDto>($"Barcode '{dto.Barcode.Trim()}' is already in use by product '{conflict.ProductName}' (SKU: {conflict.Sku}).");
            }
        }

        // Check additional barcodes for conflicts
        if (dto.AdditionalBarcodes.Count > 0)
        {
            var cleanCodes = dto.AdditionalBarcodes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList();
            foreach (var code in cleanCodes)
            {
                if (string.Equals(code, dto.Barcode?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Skip if same as primary
                }
                var conflict = await _productRepository.FindBarcodeConflictAsync(code, null, cancellationToken);
                if (conflict != null)
                {
                    return Result.Failure<ProductDto>($"Additional barcode '{code}' is already in use by product '{conflict.ProductName}' (SKU: {conflict.Sku}).");
                }
            }
        }

        // 3. Tax percent default from ShopSetting if zero
        decimal tax = dto.TaxPercent;
        if (tax <= 0)
        {
            var settings = await _shopSettingRepository.GetCurrentSettingsAsync(cancellationToken);
            if (settings != null)
            {
                tax = settings.DefaultTaxPercent;
            }
        }

        // 4. Construct Product Entity
        var product = new Product
        {
            Name = dto.Name.Trim(),
            Sku = sku,
            Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim(),
            Description = dto.Description?.Trim(),
            ImagePath = dto.ImagePath?.Trim(),
            CategoryId = dto.CategoryId,
            BrandId = dto.BrandId > 0 ? dto.BrandId : null,
            UnitId = dto.UnitId,
            CostPrice = dto.CostPrice,
            SalePrice = dto.SalePrice,
            MinSalePrice = dto.MinSalePrice,
            TaxPercent = tax,
            ReorderLevel = dto.ReorderLevel,
            TrackStock = dto.TrackStock,
            IsWeighable = dto.IsWeighable,
            AllowDecimalQuantity = dto.AllowDecimalQuantity,
            IsActive = true,
            CreatedByUserId = userId
        };

        // 5. Create in database with opening stock in atomic transaction
        await _productRepository.CreateWithOpeningStockAsync(product, dto.OpeningQuantity, dto.CostPrice, userId, cancellationToken);

        // 6. Sync additional barcodes & tier prices
        if (dto.AdditionalBarcodes.Count > 0)
        {
            await _productRepository.SyncProductBarcodesAsync(product.Id, product.Barcode, dto.AdditionalBarcodes, cancellationToken);
        }

        if (dto.ProductPrices.Count > 0)
        {
            await _productRepository.SyncProductPricesAsync(product.Id, dto.ProductPrices, cancellationToken);
        }

        // 7. Audit log
        await _auditLogService.LogAsync(
            userId,
            "CreateProduct",
            "Product",
            product.Id,
            $"Created product '{product.Name}' (SKU: {product.Sku}) with opening stock {dto.OpeningQuantity} {product.Unit?.ShortName}");

        return await GetProductByIdAsync(product.Id, cancellationToken);
    }

    public async Task<Result<ProductDto>> UpdateProductAsync(UpdateProductDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var val = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<ProductDto>(val.ToDictionary(), "Validation failed for product.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var existing = await _productRepository.GetByIdAsync(dto.Id, cancellationToken);
            if (existing == null)
            {
                return Result.Failure<ProductDto>($"Product with ID {dto.Id} was not found.");
            }

            // 1. SKU uniqueness
            string sku = dto.Sku.Trim();
            if (await _productRepository.IsSkuExistsAsync(sku, dto.Id, cancellationToken))
            {
                return Result.Failure<ProductDto>($"SKU '{sku}' is already assigned to another product.");
            }

            // Promote first additional barcode to primary if primary barcode was left empty
            if (string.IsNullOrWhiteSpace(dto.Barcode) && dto.AdditionalBarcodes.Count > 0)
            {
                var firstValid = dto.AdditionalBarcodes.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
                if (firstValid != null)
                {
                    dto.Barcode = firstValid.Trim();
                    dto.AdditionalBarcodes.Remove(firstValid);
                }
            }

            // 2. Barcode conflict validation
            if (!string.IsNullOrWhiteSpace(dto.Barcode))
            {
                var conflict = await _productRepository.FindBarcodeConflictAsync(dto.Barcode.Trim(), dto.Id, cancellationToken);
                if (conflict != null)
                {
                    return Result.Failure<ProductDto>($"Barcode '{dto.Barcode.Trim()}' is already in use by product '{conflict.ProductName}' (SKU: {conflict.Sku}).");
                }
            }

            // Check additional barcodes
            if (dto.AdditionalBarcodes.Count > 0)
            {
                var cleanCodes = dto.AdditionalBarcodes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList();
                foreach (var code in cleanCodes)
                {
                    if (string.Equals(code, dto.Barcode?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    var conflict = await _productRepository.FindBarcodeConflictAsync(code, dto.Id, cancellationToken);
                    if (conflict != null)
                    {
                        return Result.Failure<ProductDto>($"Additional barcode '{code}' is already in use by product '{conflict.ProductName}' (SKU: {conflict.Sku}).");
                    }
                }
            }

            // 3. Stock Adjustment Check (read ledger stock INSIDE transaction)
            if (dto.NewStockQuantity.HasValue)
            {
                if (dto.NewStockQuantity.Value < 0)
                {
                    return Result.Failure<ProductDto>("Stock quantity cannot be negative.");
                }

                decimal ledgerStock = await _productRepository.GetCurrentStockAsync(existing.Id, cancellationToken);
                decimal delta = dto.NewStockQuantity.Value - ledgerStock;

                if (delta != 0)
                {
                    // single-user: no permission check needed

                    if (string.IsNullOrWhiteSpace(dto.StockAdjustmentNote))
                    {
                        return Result.Failure<ProductDto>("A reason is required to adjust stock quantity.");
                    }

                    if (_stockAdjustmentService != null)
                    {
                        var adjResult = await _stockAdjustmentService.CreateAndPostSingleAdjustmentAsync(
                            existing.Id,
                            dto.NewStockQuantity.Value,
                            dto.StockAdjustmentNote.Trim(),
                            StockAdjustmentReason.CountCorrection,
                            userId,
                            cancellationToken);

                        if (!adjResult.Success)
                        {
                            return Result.Failure<ProductDto>(adjResult.ErrorMessage ?? "Failed to adjust product stock.");
                        }
                    }
                    else
                    {
                        await _productRepository.RecordStockAdjustmentAsync(
                            existing.Id,
                            delta,
                            existing.CostPrice,
                            dto.StockAdjustmentNote.Trim(),
                            userId,
                            cancellationToken);

                        await _auditLogService.LogAsync(
                            userId,
                            "AdjustStock",
                            "Product",
                            existing.Id,
                            $"Adjusted stock for product '{existing.Name}' (SKU: {existing.Sku}) from {ledgerStock:0.###} to {dto.NewStockQuantity.Value:0.###}. Reason: {dto.StockAdjustmentNote.Trim()}");
                    }
                }
            }

            // 4. Price change audit tracking: if cost or sale price changed, write to ProductPriceHistory
            if (existing.CostPrice != dto.CostPrice || existing.SalePrice != dto.SalePrice)
            {
                await _productRepository.RecordPriceChangeAsync(
                    existing.Id,
                    existing.CostPrice,
                    dto.CostPrice,
                    existing.SalePrice,
                    dto.SalePrice,
                    userId,
                    cancellationToken);
            }

            // 5. Update properties (past StockMovements remain completely untouched)
            existing.Name = dto.Name.Trim();
            existing.Sku = sku;
            existing.Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim();
            existing.Description = dto.Description?.Trim();
            existing.ImagePath = dto.ImagePath?.Trim();
            existing.CategoryId = dto.CategoryId;
            existing.BrandId = dto.BrandId > 0 ? dto.BrandId : null;
            existing.UnitId = dto.UnitId;
            existing.CostPrice = dto.CostPrice;
            existing.SalePrice = dto.SalePrice;
            existing.MinSalePrice = dto.MinSalePrice;
            existing.TaxPercent = dto.TaxPercent;
            existing.ReorderLevel = dto.ReorderLevel;
            existing.TrackStock = dto.TrackStock;
            existing.IsWeighable = dto.IsWeighable;
            existing.AllowDecimalQuantity = dto.AllowDecimalQuantity;
            existing.IsActive = dto.IsActive;

            await _productRepository.UpdateAsync(existing, cancellationToken);

            // 6. Sync barcodes and prices
            await _productRepository.SyncProductBarcodesAsync(existing.Id, existing.Barcode, dto.AdditionalBarcodes, cancellationToken);
            await _productRepository.SyncProductPricesAsync(existing.Id, dto.ProductPrices, cancellationToken);

            // 7. Audit log
            await _auditLogService.LogAsync(
                userId,
                "UpdateProduct",
                "Product",
                existing.Id,
                $"Updated product '{existing.Name}' (SKU: {existing.Sku})");

            return await GetProductByIdAsync(existing.Id, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Result> DeactivateProductAsync(int id, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product == null)
        {
            return Result.Failure($"Product with ID {id} was not found.");
        }

        product.IsActive = false;
        await _productRepository.UpdateAsync(product, cancellationToken);

        await _auditLogService.LogAsync(
            userId,
            "DeactivateProduct",
            "Product",
            product.Id,
            $"Deactivated product '{product.Name}' (SKU: {product.Sku})");

        return Result.Ok();
    }

    public async Task<Result> DeleteProductAsync(int id, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product == null)
        {
            return Result.Failure($"Product with ID {id} was not found.");
        }

        // Check reference count: stock movements or other relations
        int refCount = await _productRepository.GetReferenceCountAsync(id, cancellationToken);
        if (refCount > 0)
        {
            return Result.Failure("REFERENCED_CANNOT_DELETE");
        }

        await _productRepository.SoftDeleteAsync(id, cancellationToken);

        await _auditLogService.LogAsync(
            userId,
            "DeleteProduct",
            "Product",
            product.Id,
            $"Soft-deleted product '{product.Name}' (SKU: {product.Sku})");

        return Result.Ok();
    }

    public async Task<Result<ProductDto>> DuplicateProductAsync(int id, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var source = await _productRepository.GetWithDetailsAsync(id, cancellationToken);
        if (source == null)
        {
            return Result.Failure<ProductDto>($"Product with ID {id} was not found.");
        }

        string newSku = await _productRepository.GenerateNextSkuAsync(cancellationToken);

        var createDto = new CreateProductDto
        {
            Name = $"Copy of {source.Name}",
            Sku = newSku,
            Barcode = null, // Do not duplicate barcodes
            Description = source.Description,
            ImagePath = source.ImagePath,
            CategoryId = source.CategoryId,
            BrandId = source.BrandId,
            UnitId = source.UnitId,
            CostPrice = source.CostPrice,
            SalePrice = source.SalePrice,
            MinSalePrice = source.MinSalePrice,
            TaxPercent = source.TaxPercent,
            ReorderLevel = source.ReorderLevel,
            TrackStock = source.TrackStock,
            IsWeighable = source.IsWeighable,
            AllowDecimalQuantity = source.AllowDecimalQuantity,
            OpeningQuantity = 0m,
            AdditionalBarcodes = new(),
            ProductPrices = source.ProductPrices.Select(pp => new ProductPriceDto
            {
                PriceLevelId = pp.PriceLevelId,
                PriceLevelName = pp.PriceLevel != null ? pp.PriceLevel.Name : string.Empty,
                Price = pp.Price
            }).ToList()
        };

        return await CreateProductAsync(createDto, userId, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<ProductPriceHistoryDto>>> GetPriceHistoryAsync(int productId, CancellationToken cancellationToken = default)
    {
        var histories = await _productRepository.GetPriceHistoriesAsync(productId, cancellationToken);
        return Result.Ok(histories);
    }

    public async Task<Result<string>> GenerateNextSkuAsync(CancellationToken cancellationToken = default)
    {
        var sku = await _productRepository.GenerateNextSkuAsync(cancellationToken);
        return Result.Ok(sku);
    }

    public async Task<Result<string?>> ValidateBarcodeAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Result.Ok<string?>(null);
        }

        var conflict = await _productRepository.FindBarcodeConflictAsync(barcode.Trim(), excludeProductId, cancellationToken);
        if (conflict != null)
        {
            return Result.Failure<string?>($"Barcode '{barcode.Trim()}' is already used by '{conflict.ProductName}' (SKU: {conflict.Sku}).");
        }

        return Result.Ok<string?>(null);
    }

    public async Task<BulkOperationResult> BulkDeleteAsync(IEnumerable<int> ids, int? userId, CancellationToken cancellationToken = default)
    {
        var result = new BulkOperationResult { TotalRequested = ids.Count() };
        foreach (var id in ids)
        {
            var product = await _productRepository.GetByIdAsync(id, cancellationToken);
            if (product == null)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = $"ID {id}", Success = false, Message = "Product not found." });
                result.SkippedCount++;
                continue;
            }

            int refCount = await _productRepository.GetReferenceCountAsync(id, cancellationToken);
            if (refCount > 0)
            {
                result.Items.Add(new BulkItemResult
                {
                    Id = id,
                    Identifier = product.Name,
                    Success = false,
                    Message = $"Skipped: in use (referenced by {refCount} movements/transactions). Deactivate instead?",
                    CanDeactivateInstead = true
                });
                result.SkippedCount++;
                continue;
            }

            await _productRepository.SoftDeleteAsync(id, cancellationToken);
            await _auditLogService.LogAsync(userId, "DeleteProduct", "Product", product.Id, $"Soft-deleted product '{product.Name}' (SKU: {product.Sku})");
            result.Items.Add(new BulkItemResult { Id = id, Identifier = product.Name, Success = true });
            result.SuccessCount++;
        }
        return result;
    }

    public async Task<BulkOperationResult> BulkDeactivateAsync(IEnumerable<int> ids, int? userId, CancellationToken cancellationToken = default)
    {
        var result = new BulkOperationResult { TotalRequested = ids.Count() };
        foreach (var id in ids)
        {
            var product = await _productRepository.GetByIdAsync(id, cancellationToken);
            if (product == null) continue;

            product.IsActive = false;
            await _productRepository.UpdateAsync(product, cancellationToken);
            await _auditLogService.LogAsync(userId, "DeactivateProduct", "Product", product.Id, $"Deactivated product '{product.Name}' (SKU: {product.Sku})");
            result.Items.Add(new BulkItemResult { Id = id, Identifier = product.Name, Success = true });
            result.SuccessCount++;
        }
        return result;
    }

    public async Task<IReadOnlyList<int>> GetFilteredProductIdsAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        return await _productRepository.GetFilteredIdsAsync(query, cancellationToken);
    }

}
