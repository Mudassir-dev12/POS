namespace ShopPOS.Core.DTOs;

public class ProductListItemDto
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? BrandId { get; set; }
    public string? BrandName { get; set; }
    public int UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitShortName { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? MinSalePrice { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool TrackStock { get; set; }
    public bool IsActive { get; set; }

    public bool IsLowStock => TrackStock && CurrentStock > 0 && CurrentStock <= ReorderLevel;
    public bool IsOutOfStock => TrackStock && CurrentStock <= 0;
}

public class ProductDto
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? BrandId { get; set; }
    public string? BrandName { get; set; }
    public int UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitShortName { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? MinSalePrice { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal CurrentStock { get; set; }
    public bool TrackStock { get; set; }
    public bool IsWeighable { get; set; }
    public bool AllowDecimalQuantity { get; set; }
    public bool IsActive { get; set; }

    public List<ProductBarcodeDto> AdditionalBarcodes { get; set; } = new();
    public List<ProductPriceDto> ProductPrices { get; set; } = new();
}

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    public int UnitId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? MinSalePrice { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool TrackStock { get; set; } = true;
    public bool IsWeighable { get; set; }
    public bool AllowDecimalQuantity { get; set; }
    public decimal OpeningQuantity { get; set; } = 0m;

    public List<string> AdditionalBarcodes { get; set; } = new();
    public List<ProductPriceDto> ProductPrices { get; set; } = new();
}

public class UpdateProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    public int UnitId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? MinSalePrice { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool TrackStock { get; set; }
    public bool IsWeighable { get; set; }
    public bool AllowDecimalQuantity { get; set; }
    public bool IsActive { get; set; }

    public List<string> AdditionalBarcodes { get; set; } = new();
    public List<ProductPriceDto> ProductPrices { get; set; } = new();

    /// <summary>
    /// Optional adjusted stock quantity on hand. If set and different from current stock, a StockMovement of type Adjustment is recorded.
    /// </summary>
    public decimal? NewStockQuantity { get; set; }

    /// <summary>
    /// Optional reason/note for stock adjustment.
    /// </summary>
    public string? StockAdjustmentNote { get; set; }
}

public class ProductBarcodeDto
{
    public int Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public class ProductPriceDto
{
    public int Id { get; set; }
    public int PriceLevelId { get; set; }
    public string PriceLevelName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductPriceHistoryDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal OldCostPrice { get; set; }
    public decimal NewCostPrice { get; set; }
    public decimal OldSalePrice { get; set; }
    public decimal NewSalePrice { get; set; }
    public int? ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class ProductSearchQuery
{
    public string? SearchTerm { get; set; }
    public int? CategoryId { get; set; }
    public int? BrandId { get; set; }
    public bool? IsActive { get; set; }
    public bool LowStockOnly { get; set; }
    public string SortBy { get; set; } = "Name";
    public bool SortDescending { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

