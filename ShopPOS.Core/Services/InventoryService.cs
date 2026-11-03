using System.Text;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

public class InventoryService : IInventoryService
{
    private readonly IStockLedgerQuery _stockLedgerQuery;
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IBrandRepository _brandRepo;
    private readonly IShopSettingRepository _shopSettingRepo;
    private readonly ISupplierLedgerRepository _supplierLedgerRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IPurchaseService _purchaseService;
    private readonly IPdfReportService _pdfReportService;

    public InventoryService(
        IStockLedgerQuery stockLedgerQuery,
        IProductRepository productRepo,
        ICategoryRepository categoryRepo,
        IBrandRepository brandRepo,
        IShopSettingRepository shopSettingRepo,
        ISupplierLedgerRepository supplierLedgerRepo,
        IInventoryRepository inventoryRepo,
        IPurchaseService purchaseService,
        IPdfReportService pdfReportService)
    {
        _stockLedgerQuery = stockLedgerQuery;
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
        _brandRepo = brandRepo;
        _shopSettingRepo = shopSettingRepo;
        _supplierLedgerRepo = supplierLedgerRepo;
        _inventoryRepo = inventoryRepo;
        _purchaseService = purchaseService;
        _pdfReportService = pdfReportService;
    }

    public async Task<Result<PagedResult<StockOverviewItemDto>>> GetStockOverviewAsync(StockOverviewFilterQuery query, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        bool isCashier = string.Equals(userRole, RoleDefaults.Cashier, StringComparison.OrdinalIgnoreCase);
        var result = await _inventoryRepo.GetStockOverviewAsync(query, isCashier, cancellationToken);
        return Result<PagedResult<StockOverviewItemDto>>.Ok(result);
    }

    public async Task<Result<StockOverviewSummaryDto>> GetStockOverviewSummaryAsync(string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        bool isCashier = string.Equals(userRole, RoleDefaults.Cashier, StringComparison.OrdinalIgnoreCase);
        var result = await _inventoryRepo.GetStockOverviewSummaryAsync(isCashier, cancellationToken);
        return Result<StockOverviewSummaryDto>.Ok(result);
    }

    public async Task<Result<StockOverviewItemDto>> GetProductStockDetailAsync(int productId, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        bool isCashier = string.Equals(userRole, RoleDefaults.Cashier, StringComparison.OrdinalIgnoreCase);

        var p = await _productRepo.GetWithDetailsAsync(productId, cancellationToken);
        if (p == null)
        {
            return Result<StockOverviewItemDto>.Failure("Product not found.");
        }

        decimal stock = await _stockLedgerQuery.GetCurrentStockAsync(p.Id, cancellationToken);
        StockStatus status;
        if (!p.TrackStock) status = StockStatus.NotTracked;
        else if (stock < 0) status = StockStatus.Negative;
        else if (stock == 0) status = StockStatus.OutOfStock;
        else if (stock <= p.ReorderLevel) status = StockStatus.Low;
        else status = StockStatus.OK;

        decimal? cost = isCashier ? null : p.CostPrice;
        decimal? costVal = isCashier ? null : Math.Round(stock * p.CostPrice, 2, MidpointRounding.AwayFromZero);
        decimal saleVal = Math.Round(stock * p.SalePrice, 2, MidpointRounding.AwayFromZero);

        return Result<StockOverviewItemDto>.Ok(new StockOverviewItemDto
        {
            ProductId = p.Id,
            Sku = p.Sku,
            Barcode = p.Barcode,
            ProductName = p.Name,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? string.Empty,
            BrandId = p.BrandId,
            BrandName = p.Brand?.Name,
            UnitName = p.Unit?.Name ?? string.Empty,
            CurrentStock = stock,
            ReorderLevel = p.ReorderLevel,
            Status = status,
            CostPrice = cost,
            SalePrice = p.SalePrice,
            StockValueAtCost = costVal,
            StockValueAtSale = saleVal,
            TrackStock = p.TrackStock
        });
    }

    public async Task<Result<PagedResult<MovementHistoryItemDto>>> GetMovementHistoryAsync(MovementHistoryFilterQuery query, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var result = await _inventoryRepo.GetMovementHistoryAsync(query, cancellationToken);
        return Result<PagedResult<MovementHistoryItemDto>>.Ok(result);
    }

    public async Task<Result<IReadOnlyList<CategoryValuationDto>>> GetCategoryValuationsAsync(string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var allProducts = await _productRepo.GetAllAsync(cancellationToken);
        var activeStockProducts = allProducts.Where(p => p.IsActive && p.TrackStock).ToList();
        var stockMap = await _stockLedgerQuery.GetStockBatchAsync(activeStockProducts.Select(p => p.Id), cancellationToken);

        var categories = await _categoryRepo.GetAllAsync(cancellationToken);

        var valuations = new List<CategoryValuationDto>();
        foreach (var cat in categories.Where(c => c.IsActive))
        {
            var catProducts = activeStockProducts.Where(p => p.CategoryId == cat.Id).ToList();
            if (catProducts.Count == 0) continue;

            decimal totalUnits = 0m;
            decimal costVal = 0m;
            decimal saleVal = 0m;

            foreach (var p in catProducts)
            {
                decimal stock = stockMap.TryGetValue(p.Id, out var s) ? s : 0m;
                if (stock > 0)
                {
                    totalUnits += stock;
                    costVal += stock * p.CostPrice;
                    saleVal += stock * p.SalePrice;
                }
            }

            valuations.Add(new CategoryValuationDto
            {
                CategoryId = cat.Id,
                CategoryName = cat.Name,
                ProductCount = catProducts.Count,
                TotalUnits = totalUnits,
                ValueAtCost = Math.Round(costVal, 2, MidpointRounding.AwayFromZero),
                ValueAtSale = Math.Round(saleVal, 2, MidpointRounding.AwayFromZero)
            });
        }

        return Result<IReadOnlyList<CategoryValuationDto>>.Ok(valuations.OrderBy(v => v.CategoryName).ToList());
    }

    public async Task<Result<IReadOnlyList<ReorderItemDto>>> GetReorderListAsync(string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var settings = await _shopSettingRepo.GetCurrentSettingsAsync(cancellationToken);
        decimal multiplier = settings?.DefaultReorderMultiplier > 0 ? settings.DefaultReorderMultiplier : 2.0m;

        var allProducts = await _productRepo.GetAllAsync(cancellationToken);
        var activeStockProducts = allProducts.Where(p => p.IsActive && p.TrackStock).ToList();
        var stockMap = await _stockLedgerQuery.GetStockBatchAsync(activeStockProducts.Select(p => p.Id), cancellationToken);

        var categories = (await _categoryRepo.GetAllAsync(cancellationToken)).ToDictionary(c => c.Id, c => c.Name);

        var reorderItems = new List<ReorderItemDto>();
        foreach (var p in activeStockProducts)
        {
            decimal currentStock = stockMap.TryGetValue(p.Id, out var s) ? s : 0m;
            if (currentStock <= p.ReorderLevel)
            {
                StockStatus status;
                if (currentStock < 0) status = StockStatus.Negative;
                else if (currentStock == 0) status = StockStatus.OutOfStock;
                else status = StockStatus.Low;

                decimal target = p.ReorderLevel * multiplier;
                decimal needed = target - currentStock;
                decimal suggested = Math.Max(1m, Math.Ceiling(needed));

                reorderItems.Add(new ReorderItemDto
                {
                    ProductId = p.Id,
                    Sku = p.Sku,
                    Barcode = p.Barcode,
                    ProductName = p.Name,
                    CategoryName = categories.TryGetValue(p.CategoryId, out var cName) ? cName : string.Empty,
                    CurrentStock = currentStock,
                    ReorderLevel = p.ReorderLevel,
                    Status = status,
                    Multiplier = multiplier,
                    SuggestedQuantity = suggested,
                    CostPrice = p.CostPrice,
                    PreferredSupplierId = null,
                    PreferredSupplierName = null,
                    IsSelected = true
                });
            }
        }

        return Result<IReadOnlyList<ReorderItemDto>>.Ok(reorderItems.OrderBy(r => r.ProductName).ToList());
    }

    public async Task<Result<PurchaseDetailDto>> CreatePurchaseDraftFromReorderAsync(CreatePurchaseFromReorderDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        if (dto.SupplierId <= 0)
        {
            return Result<PurchaseDetailDto>.Failure("A supplier must be selected for the purchase draft.");
        }

        if (dto.Items.Count == 0)
        {
            return Result<PurchaseDetailDto>.Failure("No items selected for purchase draft.");
        }

        var createPurchaseDto = new CreatePurchaseDto
        {
            SupplierId = dto.SupplierId,
            PurchaseDate = DateTime.Today,
            Notes = "Generated from Inventory Reorder List",
            Items = dto.Items.Select(i => new CreatePurchaseItemDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost,
                DiscountPercent = 0m,
                TaxPercent = 0m
            }).ToList()
        };

        var purchaseResult = await _purchaseService.CreatePurchaseDraftAsync(createPurchaseDto, userId, userRole, cancellationToken);
        if (!purchaseResult.Success)
        {
            return Result<PurchaseDetailDto>.Failure(purchaseResult.ErrorMessage ?? "Failed to create purchase draft.");
        }

        return Result<PurchaseDetailDto>.Ok(purchaseResult.Data!);
    }

    public async Task<Result<byte[]>> ExportStockOverviewCsvAsync(StockOverviewFilterQuery query, string? userRole, CancellationToken cancellationToken = default)
    {
        var overviewResult = await GetStockOverviewAsync(query, userRole, cancellationToken);
        if (!overviewResult.Success)
        {
            return Result<byte[]>.Failure(overviewResult.ErrorMessage ?? "Failed to fetch stock overview for CSV export.");
        }

        bool isCashier = string.Equals(userRole, RoleDefaults.Cashier, StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        if (isCashier)
        {
            sb.AppendLine("SKU,Product Name,Barcode,Category,Unit,Current Stock,Reorder Level,Sale Price,Value at Sale,Status");
            foreach (var item in overviewResult.Data!.Items)
            {
                sb.AppendLine($"{CsvEscape(item.Sku)},{CsvEscape(item.ProductName)},{CsvEscape(item.Barcode)},{CsvEscape(item.CategoryName)},{CsvEscape(item.UnitName)},{item.CurrentStock},{item.ReorderLevel},{item.SalePrice:F2},{item.StockValueAtSale:F2},{item.Status}");
            }
        }
        else
        {
            sb.AppendLine("SKU,Product Name,Barcode,Category,Unit,Current Stock,Reorder Level,Cost Price,Sale Price,Value at Cost,Value at Sale,Status");
            foreach (var item in overviewResult.Data!.Items)
            {
                sb.AppendLine($"{CsvEscape(item.Sku)},{CsvEscape(item.ProductName)},{CsvEscape(item.Barcode)},{CsvEscape(item.CategoryName)},{CsvEscape(item.UnitName)},{item.CurrentStock},{item.ReorderLevel},{item.CostPrice?.ToString("F2")},{item.SalePrice:F2},{item.StockValueAtCost?.ToString("F2")},{item.StockValueAtSale:F2},{item.Status}");
            }
        }

        return Result<byte[]>.Ok(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<Result<byte[]>> ExportStockOverviewPdfAsync(StockOverviewFilterQuery query, string? userRole, CancellationToken cancellationToken = default)
    {
        var overviewResult = await GetStockOverviewAsync(query, userRole, cancellationToken);
        if (!overviewResult.Success)
        {
            return Result<byte[]>.Failure(overviewResult.ErrorMessage ?? "Failed to fetch stock overview for PDF export.");
        }

        var settings = await _shopSettingRepo.GetCurrentSettingsAsync(cancellationToken);
        var settingsDto = new ShopSettingDto
        {
            ShopName = settings?.ShopName ?? "ShopPOS",
            Address = settings?.Address,
            Phone = settings?.Phone,
            CurrencySymbol = settings?.CurrencySymbol ?? "Rs"
        };

        bool isCashier = string.Equals(userRole, RoleDefaults.Cashier, StringComparison.OrdinalIgnoreCase);
        var pdfBytes = await _pdfReportService.GenerateStockOverviewPdfAsync(overviewResult.Data!.Items, settingsDto, isCashier, cancellationToken);
        return Result<byte[]>.Ok(pdfBytes);
    }

    public async Task<Result<byte[]>> ExportMovementHistoryCsvAsync(MovementHistoryFilterQuery query, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var movements = await _inventoryRepo.GetAllMovementHistoryAsync(query, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Date,SKU,Product Name,Movement Type,Quantity,Unit Cost,Reference,Note");
        foreach (var m in movements)
        {
            sb.AppendLine($"{m.MovementDate:dd MMM yyyy HH:mm},{CsvEscape(m.Sku)},{CsvEscape(m.ProductName)},{m.MovementType},{m.Quantity},{m.UnitCost:F2},{CsvEscape(m.ReferenceNumber)},{CsvEscape(m.Note)}");
        }

        return Result<byte[]>.Ok(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<Result<byte[]>> ExportValuationPdfAsync(string? userRole, CancellationToken cancellationToken = default)
    {
        var valuationResult = await GetCategoryValuationsAsync(userRole, cancellationToken);
        if (!valuationResult.Success)
        {
            return Result<byte[]>.Failure(valuationResult.ErrorMessage ?? "Failed to fetch valuation for PDF export.");
        }

        var settings = await _shopSettingRepo.GetCurrentSettingsAsync(cancellationToken);
        var settingsDto = new ShopSettingDto
        {
            ShopName = settings?.ShopName ?? "ShopPOS",
            Address = settings?.Address,
            Phone = settings?.Phone,
            CurrencySymbol = settings?.CurrencySymbol ?? "Rs"
        };

        var pdfBytes = await _pdfReportService.GenerateValuationPdfAsync(valuationResult.Data!, settingsDto, cancellationToken);
        return Result<byte[]>.Ok(pdfBytes);
    }

    public async Task<Result<byte[]>> ExportValuationCsvAsync(string? userRole, CancellationToken cancellationToken = default)
    {
        var valuationResult = await GetCategoryValuationsAsync(userRole, cancellationToken);
        if (!valuationResult.Success)
        {
            return Result<byte[]>.Failure(valuationResult.ErrorMessage ?? "Failed to fetch valuation for CSV export.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("Category,Products,Units,Cost Value,Retail Value,Potential Profit,Margin %");
        foreach (var v in valuationResult.Data!)
        {
            sb.AppendLine($"{CsvEscape(v.CategoryName)},{v.ProductCount},{v.TotalUnits},{v.TotalCostValue:F2},{v.TotalRetailValue:F2},{v.PotentialProfit:F2},{v.ProfitMarginPercent:F1}%");
        }

        return Result<byte[]>.Ok(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<Result<DashboardStatsDto>> GetDashboardStatsAsync(string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        bool isCashier = string.Equals(userRole, RoleDefaults.Cashier, StringComparison.OrdinalIgnoreCase);

        var allProducts = await _productRepo.GetAllAsync(cancellationToken);
        var activeStockProducts = allProducts.Where(p => p.IsActive && p.TrackStock).ToList();
        var stockMap = await _stockLedgerQuery.GetStockBatchAsync(activeStockProducts.Select(p => p.Id), cancellationToken);

        int lowStock = 0;
        int outStock = 0;
        decimal stockValue = 0m;

        foreach (var p in activeStockProducts)
        {
            decimal stock = stockMap.TryGetValue(p.Id, out var s) ? s : 0m;
            if (stock <= 0) outStock++;
            else if (stock <= p.ReorderLevel) lowStock++;

            if (!isCashier && stock > 0)
            {
                stockValue += stock * p.CostPrice;
            }
        }

        decimal payableTotal = 0m;
        int purchasesCount = 0;
        decimal purchasesTotal = 0m;

        if (!isCashier)
        {
            payableTotal = await _supplierLedgerRepo.GetTotalPayableBalanceAsync(cancellationToken);
            var (cnt, tot) = await _inventoryRepo.GetPurchasesThisMonthStatsAsync(cancellationToken);
            purchasesCount = cnt;
            purchasesTotal = tot;
        }

        return Result<DashboardStatsDto>.Ok(new DashboardStatsDto
        {
            TotalProducts = allProducts.Count(p => p.IsActive),
            OutOfStockCount = outStock,
            LowStockCount = lowStock,
            StockValueAtCost = Math.Round(stockValue, 2, MidpointRounding.AwayFromZero),
            SupplierPayableTotal = Math.Round(payableTotal, 2, MidpointRounding.AwayFromZero),
            PurchasesThisMonthCount = purchasesCount,
            PurchasesThisMonthAmount = Math.Round(purchasesTotal, 2, MidpointRounding.AwayFromZero)
        });
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
