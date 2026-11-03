using FluentValidation;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

public class PurchaseService : IPurchaseService
{
    private readonly ITransactionRunner _transactionRunner;
    private readonly IPurchaseRepository _purchaseRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopSettingRepository _shopSettingRepo;
    private readonly INumberSequenceService _sequenceService;
    private readonly IAuditLogService _auditLogService;
    private readonly IPdfReportService _pdfReportService;
    private readonly IValidator<CreatePurchaseDto> _createValidator;
    private readonly IValidator<UpdatePurchaseDto> _updateValidator;

    public PurchaseService(
        ITransactionRunner transactionRunner,
        IPurchaseRepository purchaseRepo,
        ISupplierRepository supplierRepo,
        IProductRepository productRepo,
        IShopSettingRepository shopSettingRepo,
        INumberSequenceService sequenceService,
        IAuditLogService auditLogService,
        IPdfReportService pdfReportService,
        IValidator<CreatePurchaseDto> createValidator,
        IValidator<UpdatePurchaseDto> updateValidator)
    {
        _transactionRunner = transactionRunner;
        _purchaseRepo = purchaseRepo;
        _supplierRepo = supplierRepo;
        _productRepo = productRepo;
        _shopSettingRepo = shopSettingRepo;
        _sequenceService = sequenceService;
        _auditLogService = auditLogService;
        _pdfReportService = pdfReportService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<Result<PagedResult<PurchaseListDto>>> GetPurchasesAsync(PurchaseSearchQuery query, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var result = await _purchaseRepo.GetPurchasesAsync(query, cancellationToken);
        return Result.Ok(result);
    }

    public async Task<Result<PurchaseDetailDto>> GetPurchaseByIdAsync(int id, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var detail = await _purchaseRepo.GetPurchaseDetailAsync(id, cancellationToken);
        if (detail == null)
        {
            return Result.Failure<PurchaseDetailDto>($"Purchase with ID {id} was not found.");
        }

        return Result.Ok(detail);
    }

    public async Task<Result<PurchaseDetailDto>> CreatePurchaseDraftAsync(CreatePurchaseDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var val = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<PurchaseDetailDto>(val.ToDictionary(), "Validation failed for purchase.");
        }

        var supplier = await _supplierRepo.GetByIdAsync(dto.SupplierId, cancellationToken);
        if (supplier == null || !supplier.IsActive)
        {
            return Result.Failure<PurchaseDetailDto>("Selected supplier is invalid or inactive.");
        }

        if (!string.IsNullOrWhiteSpace(dto.SupplierInvoiceNo))
        {
            bool duplicate = await _purchaseRepo.IsSupplierInvoiceNoUsedAsync(dto.SupplierId, dto.SupplierInvoiceNo.Trim(), null, cancellationToken);
            if (duplicate)
            {
                return Result.Failure<PurchaseDetailDto>($"Supplier invoice number '{dto.SupplierInvoiceNo.Trim()}' has already been used for this supplier.");
            }
        }

        string purNumber = await _sequenceService.GetNextNumberAsync("PUR", cancellationToken);

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = (await _purchaseRepo.GetProductsByIdsAsync(productIds, cancellationToken)).ToDictionary(p => p.Id);

        var purchase = new Purchase
        {
            PurchaseNumber = purNumber,
            SupplierId = dto.SupplierId,
            SupplierInvoiceNo = string.IsNullOrWhiteSpace(dto.SupplierInvoiceNo) ? null : dto.SupplierInvoiceNo.Trim(),
            PurchaseDate = dto.PurchaseDate.Date,
            DueDate = dto.DueDate?.Date,
            Status = PurchaseStatus.Draft,
            Notes = dto.Notes?.Trim(),
            CreatedByUserId = userId
        };

        var lineResults = new List<LineCalculationResult>();
        foreach (var itemDto in dto.Items)
        {
            if (!products.TryGetValue(itemDto.ProductId, out var prod))
            {
                return Result.Failure<PurchaseDetailDto>($"Product ID {itemDto.ProductId} was not found.");
            }

            var lineCalc = PurchaseCalculator.CalculateLine(itemDto.Quantity, itemDto.UnitCost, itemDto.DiscountPercent, itemDto.TaxPercent);
            lineResults.Add(lineCalc);

            purchase.Items.Add(new PurchaseItem
            {
                ProductId = itemDto.ProductId,
                ProductName = prod.Name,
                Sku = prod.Sku,
                Quantity = itemDto.Quantity,
                UnitCost = itemDto.UnitCost,
                DiscountPercent = itemDto.DiscountPercent,
                TaxPercent = itemDto.TaxPercent,
                LineTotal = lineCalc.LineTotal,
                Note = itemDto.Note?.Trim(),
                CreatedByUserId = userId
            });
        }

        var totals = PurchaseCalculator.CalculateTotals(lineResults, dto.DiscountAmount, dto.FreightAmount);
        purchase.SubTotal = totals.SubTotal;
        purchase.DiscountAmount = totals.DiscountAmount;
        purchase.TaxAmount = totals.TaxAmount;
        purchase.FreightAmount = totals.FreightAmount;
        purchase.GrandTotal = totals.GrandTotal;
        purchase.PaidAmount = 0m;

        await _purchaseRepo.CreatePurchaseAsync(purchase, cancellationToken);

        await _auditLogService.LogAsync(
            userId,
            "CreatePurchaseDraft",
            "Purchase",
            purchase.Id,
            $"Created purchase draft {purchase.PurchaseNumber} for supplier '{supplier.Name}', total {purchase.GrandTotal}");

        var warnings = new List<string>();
        foreach (var itemDto in dto.Items)
        {
            if (products.TryGetValue(itemDto.ProductId, out var prod) && prod.CostPrice > 0m)
            {
                decimal diff = Math.Abs(itemDto.UnitCost - prod.CostPrice);
                decimal pctDiff = (diff / prod.CostPrice) * 100m;
                if (pctDiff > 30m)
                {
                    warnings.Add($"Unit cost for '{prod.Name}' ({ShopPOS.Core.Services.MoneyFormatter.Instance.Format(itemDto.UnitCost)}) differs by {pctDiff:F1}% from current cost ({ShopPOS.Core.Services.MoneyFormatter.Instance.Format(prod.CostPrice)}).");
                }
            }
        }

        var detailRes = await GetPurchaseByIdAsync(purchase.Id, userRole, cancellationToken);
        if (detailRes.Success && detailRes.Data != null)
        {
            detailRes.Data.Warnings = warnings;
        }
        return detailRes;
    }

    public async Task<Result<PurchaseDetailDto>> UpdatePurchaseDraftAsync(UpdatePurchaseDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var val = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<PurchaseDetailDto>(val.ToDictionary(), "Validation failed for purchase.");
        }

        var purchase = await _purchaseRepo.GetPurchaseByIdAsync(dto.Id, true, cancellationToken);
        if (purchase == null)
        {
            return Result.Failure<PurchaseDetailDto>($"Purchase with ID {dto.Id} was not found.");
        }

        if (purchase.Status != PurchaseStatus.Draft)
        {
            return Result.Failure<PurchaseDetailDto>("Only draft purchases can be edited. Posted or cancelled purchases are immutable.");
        }

        var supplier = await _supplierRepo.GetByIdAsync(dto.SupplierId, cancellationToken);
        if (supplier == null || !supplier.IsActive)
        {
            return Result.Failure<PurchaseDetailDto>("Selected supplier is invalid or inactive.");
        }

        if (!string.IsNullOrWhiteSpace(dto.SupplierInvoiceNo))
        {
            bool duplicate = await _purchaseRepo.IsSupplierInvoiceNoUsedAsync(dto.SupplierId, dto.SupplierInvoiceNo.Trim(), dto.Id, cancellationToken);
            if (duplicate)
            {
                return Result.Failure<PurchaseDetailDto>($"Supplier invoice number '{dto.SupplierInvoiceNo.Trim()}' has already been used for this supplier.");
            }
        }

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = (await _purchaseRepo.GetProductsByIdsAsync(productIds, cancellationToken)).ToDictionary(p => p.Id);

        purchase.SupplierId = dto.SupplierId;
        purchase.SupplierInvoiceNo = string.IsNullOrWhiteSpace(dto.SupplierInvoiceNo) ? null : dto.SupplierInvoiceNo.Trim();
        purchase.PurchaseDate = dto.PurchaseDate.TimeOfDay == TimeSpan.Zero ? dto.PurchaseDate.Date.Add(DateTime.Now.TimeOfDay) : dto.PurchaseDate;
        purchase.DueDate = dto.DueDate?.Date;
        purchase.Notes = dto.Notes?.Trim();

        purchase.Items.Clear();

        var lineResults = new List<LineCalculationResult>();
        foreach (var itemDto in dto.Items)
        {
            if (!products.TryGetValue(itemDto.ProductId, out var prod))
            {
                return Result.Failure<PurchaseDetailDto>($"Product ID {itemDto.ProductId} was not found.");
            }

            var lineCalc = PurchaseCalculator.CalculateLine(itemDto.Quantity, itemDto.UnitCost, itemDto.DiscountPercent, itemDto.TaxPercent);
            lineResults.Add(lineCalc);

            purchase.Items.Add(new PurchaseItem
            {
                PurchaseId = purchase.Id,
                ProductId = itemDto.ProductId,
                ProductName = prod.Name,
                Sku = prod.Sku,
                Quantity = itemDto.Quantity,
                UnitCost = itemDto.UnitCost,
                DiscountPercent = itemDto.DiscountPercent,
                TaxPercent = itemDto.TaxPercent,
                LineTotal = lineCalc.LineTotal,
                Note = itemDto.Note?.Trim(),
                CreatedByUserId = userId
            });
        }

        var totals = PurchaseCalculator.CalculateTotals(lineResults, dto.DiscountAmount, dto.FreightAmount);
        purchase.SubTotal = totals.SubTotal;
        purchase.DiscountAmount = totals.DiscountAmount;
        purchase.TaxAmount = totals.TaxAmount;
        purchase.FreightAmount = totals.FreightAmount;
        purchase.GrandTotal = totals.GrandTotal;

        await _purchaseRepo.UpdatePurchaseAsync(purchase, cancellationToken);

        await _auditLogService.LogAsync(
            userId,
            "UpdatePurchaseDraft",
            "Purchase",
            purchase.Id,
            $"Updated purchase draft {purchase.PurchaseNumber}, new total {purchase.GrandTotal}");

        var warnings = new List<string>();
        foreach (var itemDto in dto.Items)
        {
            if (products.TryGetValue(itemDto.ProductId, out var prod) && prod.CostPrice > 0m)
            {
                decimal diff = Math.Abs(itemDto.UnitCost - prod.CostPrice);
                decimal pctDiff = (diff / prod.CostPrice) * 100m;
                if (pctDiff > 30m)
                {
                    warnings.Add($"Unit cost for '{prod.Name}' ({ShopPOS.Core.Services.MoneyFormatter.Instance.Format(itemDto.UnitCost)}) differs by {pctDiff:F1}% from current cost ({ShopPOS.Core.Services.MoneyFormatter.Instance.Format(prod.CostPrice)}).");
                }
            }
        }

        var detailRes = await GetPurchaseByIdAsync(purchase.Id, userRole, cancellationToken);
        if (detailRes.Success && detailRes.Data != null)
        {
            detailRes.Data.Warnings = warnings;
        }
        return detailRes;
    }

    public async Task<Result<bool>> DeletePurchaseDraftAsync(int id, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var purchase = await _purchaseRepo.GetPurchaseByIdAsync(id, false, cancellationToken);
        if (purchase == null)
        {
            return Result.Failure<bool>($"Purchase with ID {id} was not found.");
        }

        if (purchase.Status != PurchaseStatus.Draft)
        {
            return Result.Failure<bool>("Only draft purchases can be deleted. Posted purchases cannot be deleted; use Cancel instead.");
        }

        await _purchaseRepo.SoftDeletePurchaseAsync(id, cancellationToken);

        await _auditLogService.LogAsync(
            userId,
            "DeletePurchaseDraft",
            "Purchase",
            id,
            $"Deleted draft purchase {purchase.PurchaseNumber}");

        return Result.Ok(true);
    }

    public async Task<Result<PurchaseDetailDto>> PostPurchaseAsync(int purchaseId, decimal? payNowAmount, PaymentMethod? payNowMethod, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            var purchase = await _purchaseRepo.GetPurchaseByIdAsync(purchaseId, true, cancellationToken);
            if (purchase == null)
            {
                return Result.Failure<PurchaseDetailDto>($"Purchase with ID {purchaseId} was not found.");
            }

            if (purchase.Status != PurchaseStatus.Draft)
            {
                return Result.Failure<PurchaseDetailDto>($"Only draft purchases can be posted. Current status: {purchase.Status}.");
            }

            if (purchase.Supplier == null || !purchase.Supplier.IsActive)
            {
                return Result.Failure<PurchaseDetailDto>("Cannot post purchase. Supplier is inactive or missing.");
            }

            if (purchase.PurchaseDate.Date > DateTime.Today.AddDays(1))
            {
                return Result.Failure<PurchaseDetailDto>("Cannot post purchase. Purchase date cannot be more than 1 day in the future.");
            }

            if (purchase.Items.Count == 0)
            {
                return Result.Failure<PurchaseDetailDto>("Cannot post purchase without line items.");
            }

            foreach (var item in purchase.Items)
            {
                if (item.Product == null || !item.Product.IsActive)
                {
                    return Result.Failure<PurchaseDetailDto>($"Product '{item.ProductName}' is inactive or deleted.");
                }

                if (item.Quantity <= 0)
                {
                    return Result.Failure<PurchaseDetailDto>($"Quantity for '{item.ProductName}' must be greater than zero.");
                }

                if (item.UnitCost < 0)
                {
                    return Result.Failure<PurchaseDetailDto>($"Unit cost for '{item.ProductName}' cannot be negative.");
                }

                if (string.IsNullOrWhiteSpace(item.ProductName)) item.ProductName = item.Product.Name;
                if (string.IsNullOrWhiteSpace(item.Sku)) item.Sku = item.Product.Sku;
            }

            var settings = await _shopSettingRepo.GetCurrentSettingsAsync(cancellationToken);
            var costingMethod = settings?.CostingMethod ?? CostingMethod.WeightedAverage;

            foreach (var item in purchase.Items)
            {
                var calc = PurchaseCalculator.CalculateLine(item.Quantity, item.UnitCost, item.DiscountPercent, item.TaxPercent);
                decimal effectiveNetCost = calc.EffectiveNetUnitCost;

                var product = await _productRepo.GetByIdAsync(item.ProductId, cancellationToken) ?? item.Product!;
                decimal currentCost = product.CostPrice;
                decimal newCostPrice;

                if (costingMethod == CostingMethod.LastCost)
                {
                    newCostPrice = Math.Round(effectiveNetCost, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    decimal currentStock = await _purchaseRepo.GetProductCurrentStockAsync(product.Id, cancellationToken);
                    if (currentStock <= 0m)
                    {
                        newCostPrice = Math.Round(effectiveNetCost, 2, MidpointRounding.AwayFromZero);
                    }
                    else
                    {
                        decimal totalExistingValue = currentStock * currentCost;
                        decimal totalNewValue = item.Quantity * effectiveNetCost;
                        decimal totalStock = currentStock + item.Quantity;
                        newCostPrice = Math.Round((totalExistingValue + totalNewValue) / totalStock, 2, MidpointRounding.AwayFromZero);
                    }
                }

                if (newCostPrice != currentCost)
                {
                    await _purchaseRepo.UpdateProductCostPriceAsync(product.Id, newCostPrice, currentCost, userId, cancellationToken);
                }

                var movement = new StockMovement
                {
                    ProductId = item.ProductId,
                    MovementType = StockMovementType.Purchase,
                    Quantity = item.Quantity,
                    UnitCost = effectiveNetCost,
                    ReferenceType = "Purchase",
                    ReferenceId = purchase.Id,
                    MovementDate = purchase.PurchaseDate.TimeOfDay == TimeSpan.Zero ? purchase.PurchaseDate.Date.Add(DateTime.Now.TimeOfDay) : purchase.PurchaseDate,
                    Note = $"Purchase {purchase.PurchaseNumber}",
                    CreatedByUserId = userId
                };
                await _purchaseRepo.AddStockMovementAsync(movement, cancellationToken);
            }

            if (payNowAmount.HasValue && payNowAmount.Value > 0m)
            {
                decimal amountToPay = Math.Round(payNowAmount.Value, 2, MidpointRounding.AwayFromZero);
                if (amountToPay > purchase.GrandTotal)
                {
                    return Result.Failure<PurchaseDetailDto>($"Pay now amount ({ShopPOS.Core.Services.MoneyFormatter.Instance.Format(amountToPay)}) cannot exceed grand total ({ShopPOS.Core.Services.MoneyFormatter.Instance.Format(purchase.GrandTotal)}).");
                }

                string spayNumber = await _sequenceService.GetNextNumberAsync("SPAY", cancellationToken);
                var payment = new SupplierPayment
                {
                    PaymentNumber = spayNumber,
                    SupplierId = purchase.SupplierId,
                    PurchaseId = purchase.Id,
                    PaymentDate = purchase.PurchaseDate,
                    Amount = amountToPay,
                    Method = payNowMethod ?? PaymentMethod.Cash,
                    Reference = $"Immediate payment on {purchase.PurchaseNumber}",
                    Notes = "Paid during purchase invoice posting",
                    CreatedByUserId = userId
                };
                await _purchaseRepo.AddSupplierPaymentAsync(payment, cancellationToken);
                purchase.PaidAmount = amountToPay;
            }

            purchase.Status = PurchaseStatus.Posted;
            purchase.PostedAt = DateTime.UtcNow;
            purchase.PostedByUserId = userId;

            await _purchaseRepo.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                userId,
                "PostPurchase",
                "Purchase",
                purchase.Id,
                $"Posted purchase {purchase.PurchaseNumber} (Total: {purchase.GrandTotal}, Paid: {purchase.PaidAmount})");

            return await GetPurchaseByIdAsync(purchase.Id, userRole, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Result<bool>> CancelPurchaseAsync(int purchaseId, string reason, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<bool>("A cancellation reason is required.");
        }

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            var purchase = await _purchaseRepo.GetPurchaseByIdAsync(purchaseId, true, cancellationToken);
            if (purchase == null)
            {
                return Result.Failure<bool>($"Purchase with ID {purchaseId} was not found.");
            }

            if (purchase.Status != PurchaseStatus.Posted)
            {
                return Result.Failure<bool>($"Only posted purchases can be cancelled. Current status: {purchase.Status}.");
            }

            bool hasActivePayments = await _purchaseRepo.HasActivePaymentsForPurchaseAsync(purchase.Id, cancellationToken);
            if (hasActivePayments)
            {
                return Result.Failure<bool>("Cannot cancel this purchase because active payments are allocated to it. Please cancel all linked payments first.");
            }

            bool hasActiveReturns = await _purchaseRepo.HasActiveReturnsForPurchaseAsync(purchase.Id, cancellationToken);
            if (hasActiveReturns)
            {
                return Result.Failure<bool>("Cannot cancel this purchase because posted returns are linked to it.");
            }

            var settings = await _shopSettingRepo.GetCurrentSettingsAsync(cancellationToken);
            bool allowNegative = settings?.AllowNegativeStock ?? false;

            if (!allowNegative)
            {
                foreach (var item in purchase.Items)
                {
                    decimal currentStock = await _purchaseRepo.GetProductCurrentStockAsync(item.ProductId, cancellationToken);
                    if (currentStock - item.Quantity < 0)
                    {
                        return Result.Failure<bool>($"Cannot cancel purchase. Cancelling would reduce stock of '{item.ProductName}' below zero (Current stock: {currentStock:G29}, Required to deduct: {item.Quantity:G29}).");
                    }
                }
            }

            foreach (var item in purchase.Items)
            {
                var reversal = new StockMovement
                {
                    ProductId = item.ProductId,
                    MovementType = StockMovementType.PurchaseCancel,
                    Quantity = -item.Quantity,
                    UnitCost = item.UnitCost,
                    ReferenceType = "PurchaseCancel",
                    ReferenceId = purchase.Id,
                    MovementDate = DateTime.Now,
                    Note = $"Reversal for cancelled purchase {purchase.PurchaseNumber}: {reason.Trim()}",
                    CreatedByUserId = userId
                };
                await _purchaseRepo.AddStockMovementAsync(reversal, cancellationToken);
            }

            purchase.Status = PurchaseStatus.Cancelled;
            purchase.CancelledAt = DateTime.UtcNow;
            purchase.CancelledByUserId = userId;
            purchase.CancelReason = reason.Trim();

            await _purchaseRepo.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                userId,
                "CancelPurchase",
                "Purchase",
                purchase.Id,
                $"Cancelled purchase {purchase.PurchaseNumber}. Reason: {reason.Trim()}");

            return Result.Ok(true);
        }, cancellationToken);
    }

    public async Task<Result<byte[]>> ExportPurchasePdfAsync(int purchaseId, string? userRole, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var detail = await _purchaseRepo.GetPurchaseDetailAsync(purchaseId, cancellationToken);
        if (detail == null)
        {
            return Result.Failure<byte[]>($"Purchase with ID {purchaseId} was not found.");
        }

        var settings = await _shopSettingRepo.GetCurrentSettingsAsync(cancellationToken);
        var settingsDto = new ShopSettingDto
        {
            ShopName = settings?.ShopName ?? "Shop POS",
            Address = settings?.Address,
            Phone = settings?.Phone,
            TaxNumber = settings?.TaxNumber,
            CurrencySymbol = settings?.CurrencySymbol ?? "Rs"
        };

        var bytes = await _pdfReportService.GeneratePurchaseInvoicePdfAsync(detail, settingsDto, cancellationToken);
        return Result.Ok(bytes);
    }

    public async Task<Result<decimal>> GetProductCurrentStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        var stock = await _purchaseRepo.GetProductCurrentStockAsync(productId, cancellationToken);
        return Result.Ok(stock);
    }

    public async Task<Result<decimal>> GetProductLastCostAsync(int productId, CancellationToken cancellationToken = default)
    {
        var cost = await _purchaseRepo.GetProductLastCostAsync(productId, cancellationToken);
        return Result.Ok(cost);
    }

    public async Task<BulkOperationResult> BulkDeleteDraftsAsync(IEnumerable<int> ids, int? userId, CancellationToken cancellationToken = default)
    {
        var result = new BulkOperationResult { TotalRequested = ids.Count() };
        foreach (var id in ids)
        {
            var p = await _purchaseRepo.GetPurchaseByIdAsync(id, includeItems: false, cancellationToken);
            if (p == null)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = $"ID {id}", Success = false, Message = "Purchase not found." });
                result.SkippedCount++;
                continue;
            }
            if (p.Status != PurchaseStatus.Draft)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = p.PurchaseNumber, Success = false, Message = "Cannot delete posted or cancelled purchase. Cancel instead." });
                result.SkippedCount++;
                continue;
            }

            var delRes = await DeletePurchaseDraftAsync(id, userId, null, cancellationToken);
            if (delRes.Success)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = p.PurchaseNumber, Success = true });
                result.SuccessCount++;
            }
            else
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = p.PurchaseNumber, Success = false, Message = delRes.ErrorMessage ?? "Delete failed." });
                result.SkippedCount++;
            }
        }
        return result;
    }

    public async Task<BulkOperationResult> BulkCancelPurchasesAsync(IEnumerable<int> ids, string reason, int? userId, CancellationToken cancellationToken = default)
    {
        var result = new BulkOperationResult { TotalRequested = ids.Count() };
        foreach (var id in ids)
        {
            var p = await _purchaseRepo.GetPurchaseByIdAsync(id, includeItems: false, cancellationToken);
            if (p == null)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = $"ID {id}", Success = false, Message = "Purchase not found." });
                result.SkippedCount++;
                continue;
            }

            var cancelRes = await CancelPurchaseAsync(id, reason, userId, null, cancellationToken);
            if (cancelRes.Success)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = p.PurchaseNumber, Success = true });
                result.SuccessCount++;
            }
            else
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = p.PurchaseNumber, Success = false, Message = cancelRes.ErrorMessage ?? "Cannot cancel purchase." });
                result.SkippedCount++;
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<int>> GetFilteredPurchaseIdsAsync(PurchaseSearchQuery query, CancellationToken cancellationToken = default)
    {
        return await _purchaseRepo.GetFilteredIdsAsync(query, cancellationToken);
    }

}
