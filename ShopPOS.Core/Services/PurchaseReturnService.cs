using FluentValidation;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

public class PurchaseReturnService : IPurchaseReturnService
{
    private readonly ITransactionRunner _transactionRunner;
    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly IPurchaseRepository _purchaseRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly IShopSettingRepository _shopSettingRepo;
    private readonly INumberSequenceService _sequenceService;
    private readonly IAuditLogService _auditLogService;
    private readonly IPermissionService _permissionService;
    private readonly IValidator<CreatePurchaseReturnDto> _validator;

    public PurchaseReturnService(
        ITransactionRunner transactionRunner,
        IPurchaseReturnRepository returnRepo,
        IPurchaseRepository purchaseRepo,
        ISupplierRepository supplierRepo,
        IShopSettingRepository shopSettingRepo,
        INumberSequenceService sequenceService,
        IAuditLogService auditLogService,
        IPermissionService permissionService,
        IValidator<CreatePurchaseReturnDto> validator)
    {
        _transactionRunner = transactionRunner;
        _returnRepo = returnRepo;
        _purchaseRepo = purchaseRepo;
        _supplierRepo = supplierRepo;
        _shopSettingRepo = shopSettingRepo;
        _sequenceService = sequenceService;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
        _validator = validator;
    }

    public async Task<Result<PagedResult<PurchaseReturnDto>>> GetReturnsAsync(int? supplierId, int pageNumber, int pageSize, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManagePurchases(userRole))
        {
            return Result.Failure<PagedResult<PurchaseReturnDto>>("Permission denied. You do not have access to Purchase Returns.");
        }

        var result = await _returnRepo.GetReturnsAsync(supplierId, pageNumber, pageSize, cancellationToken);
        return Result.Ok(result);
    }

    public async Task<Result<PurchaseReturnDto>> PostReturnAsync(CreatePurchaseReturnDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManagePurchases(userRole))
        {
            return Result.Failure<PurchaseReturnDto>("Permission denied. Only Admins and Managers can post purchase returns.");
        }

        var val = await _validator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<PurchaseReturnDto>(val.ToDictionary(), "Validation failed for purchase return.");
        }

        var supplier = await _supplierRepo.GetByIdAsync(dto.SupplierId, cancellationToken);
        if (supplier == null || !supplier.IsActive)
        {
            return Result.Failure<PurchaseReturnDto>("Selected supplier is invalid or inactive.");
        }

        Purchase? purchase = null;
        if (dto.PurchaseId.HasValue && dto.PurchaseId.Value > 0)
        {
            purchase = await _purchaseRepo.GetPurchaseByIdAsync(dto.PurchaseId.Value, true, cancellationToken);
            if (purchase == null)
            {
                return Result.Failure<PurchaseReturnDto>($"Purchase ID {dto.PurchaseId.Value} was not found.");
            }

            if (purchase.Status != PurchaseStatus.Posted)
            {
                return Result.Failure<PurchaseReturnDto>("Returns can only be linked to Posted purchase invoices.");
            }

            if (purchase.SupplierId != dto.SupplierId)
            {
                return Result.Failure<PurchaseReturnDto>("Selected purchase belongs to a different supplier.");
            }
        }

        var settings = await _shopSettingRepo.GetCurrentSettingsAsync(cancellationToken);
        bool allowNegative = settings?.AllowNegativeStock ?? false;

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = (await _returnRepo.GetProductsByIdsAsync(productIds, cancellationToken)).ToDictionary(p => p.Id);

        foreach (var itemDto in dto.Items)
        {
            if (!products.TryGetValue(itemDto.ProductId, out var prod))
            {
                return Result.Failure<PurchaseReturnDto>($"Product ID {itemDto.ProductId} was not found.");
            }

            if (purchase != null)
            {
                var purLine = purchase.Items.FirstOrDefault(i => !i.IsDeleted && i.ProductId == itemDto.ProductId);
                if (purLine == null)
                {
                    return Result.Failure<PurchaseReturnDto>($"Product '{prod.Name}' was not on purchase {purchase.PurchaseNumber}.");
                }

                decimal alreadyReturned = await _returnRepo.GetTotalReturnedQuantityAsync(purchase.Id, itemDto.ProductId, cancellationToken);
                decimal maxReturnable = purLine.Quantity - alreadyReturned;

                if (itemDto.Quantity > maxReturnable)
                {
                    return Result.Failure<PurchaseReturnDto>($"Cannot return {itemDto.Quantity:G29} of '{prod.Name}'. Maximum returnable quantity from invoice {purchase.PurchaseNumber} is {maxReturnable:G29} (Purchased: {purLine.Quantity:G29}, Previously returned: {alreadyReturned:G29}).");
                }
            }

            if (!allowNegative)
            {
                decimal currentStock = await _purchaseRepo.GetProductCurrentStockAsync(itemDto.ProductId, cancellationToken);
                if (currentStock - itemDto.Quantity < 0)
                {
                    return Result.Failure<PurchaseReturnDto>($"Cannot return product '{prod.Name}'. Current stock ({currentStock:G29}) is insufficient to return {itemDto.Quantity:G29}.");
                }
            }
        }

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            string returnNumber = await _sequenceService.GetNextNumberAsync("PRET", cancellationToken);

            var returnEntity = new PurchaseReturn
            {
                ReturnNumber = returnNumber,
                SupplierId = dto.SupplierId,
                PurchaseId = dto.PurchaseId > 0 ? dto.PurchaseId : null,
                ReturnDate = dto.ReturnDate.TimeOfDay == TimeSpan.Zero ? dto.ReturnDate.Date.Add(DateTime.Now.TimeOfDay) : dto.ReturnDate,
                Notes = dto.Notes?.Trim(),
                Status = PurchaseReturnStatus.Posted,
                PostedAt = DateTime.UtcNow,
                PostedByUserId = userId,
                CreatedByUserId = userId
            };

            decimal grandTotal = 0m;
            foreach (var itemDto in dto.Items)
            {
                var prod = products[itemDto.ProductId];
                decimal lineTotal = PurchaseCalculator.Round(itemDto.Quantity * itemDto.UnitCost);
                grandTotal += lineTotal;

                returnEntity.Items.Add(new PurchaseReturnItem
                {
                    ProductId = itemDto.ProductId,
                    ProductName = prod.Name,
                    Sku = prod.Sku,
                    Quantity = itemDto.Quantity,
                    UnitCost = itemDto.UnitCost,
                    LineTotal = lineTotal,
                    Note = itemDto.Note?.Trim(),
                    CreatedByUserId = userId
                });
            }

            returnEntity.TotalAmount = grandTotal;
            await _returnRepo.CreateReturnAsync(returnEntity, cancellationToken);

            foreach (var item in returnEntity.Items)
            {
                var movement = new StockMovement
                {
                    ProductId = item.ProductId,
                    MovementType = StockMovementType.PurchaseReturn,
                    Quantity = -item.Quantity,
                    UnitCost = item.UnitCost,
                    ReferenceType = "PurchaseReturn",
                    ReferenceId = returnEntity.Id,
                    MovementDate = returnEntity.ReturnDate.TimeOfDay == TimeSpan.Zero ? returnEntity.ReturnDate.Date.Add(DateTime.Now.TimeOfDay) : returnEntity.ReturnDate,
                    Note = $"Purchase Return {returnEntity.ReturnNumber}",
                    CreatedByUserId = userId
                };
                await _returnRepo.AddStockMovementAsync(movement, cancellationToken);
            }

            await _returnRepo.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                userId,
                "PostPurchaseReturn",
                "PurchaseReturn",
                returnEntity.Id,
                $"Posted return {returnEntity.ReturnNumber} to supplier '{supplier.Name}', total amount {returnEntity.TotalAmount:N2}");

            var returnDto = new PurchaseReturnDto
            {
                Id = returnEntity.Id,
                ReturnNumber = returnEntity.ReturnNumber,
                SupplierId = returnEntity.SupplierId,
                SupplierName = supplier.Name,
                PurchaseId = returnEntity.PurchaseId,
                PurchaseNumber = purchase?.PurchaseNumber,
                ReturnDate = returnEntity.ReturnDate,
                TotalAmount = returnEntity.TotalAmount,
                Notes = returnEntity.Notes,
                Status = returnEntity.Status,
                Items = returnEntity.Items.Select(i => new PurchaseReturnItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Sku = i.Sku,
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost,
                    LineTotal = i.LineTotal,
                    Note = i.Note
                }).ToList()
            };

            return Result.Ok(returnDto);
        }, cancellationToken);
    }

    public async Task<Result<bool>> CancelPurchaseReturnAsync(int returnId, string reason, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManagePurchases(userRole))
        {
            return Result.Failure<bool>("Permission denied. Only Admins and Managers can cancel purchase returns.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<bool>("A reason is required to cancel a purchase return.");
        }

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            var returnEntity = await _returnRepo.GetReturnByIdAsync(returnId, cancellationToken);
            if (returnEntity == null)
            {
                return Result.Failure<bool>($"Purchase return with ID {returnId} was not found.");
            }

            if (returnEntity.Status != PurchaseReturnStatus.Posted)
            {
                return Result.Failure<bool>($"Only posted purchase returns can be cancelled. Current status: {returnEntity.Status}.");
            }

            foreach (var item in returnEntity.Items)
            {
                var reversal = new StockMovement
                {
                    ProductId = item.ProductId,
                    MovementType = StockMovementType.PurchaseReturnCancel,
                    Quantity = item.Quantity, // Reverses the negative return quantity
                    UnitCost = item.UnitCost,
                    ReferenceType = "PurchaseReturnCancel",
                    ReferenceId = returnEntity.Id,
                    MovementDate = DateTime.Now,
                    Note = $"Reversal for cancelled return {returnEntity.ReturnNumber}: {reason.Trim()}",
                    CreatedByUserId = userId
                };
                await _returnRepo.AddStockMovementAsync(reversal, cancellationToken);
            }

            returnEntity.Status = PurchaseReturnStatus.Cancelled;
            returnEntity.CancelledAt = DateTime.UtcNow;
            returnEntity.CancelledByUserId = userId;
            returnEntity.CancelReason = reason.Trim();

            await _returnRepo.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                userId,
                "CancelPurchaseReturn",
                "PurchaseReturn",
                returnEntity.Id,
                $"Cancelled purchase return {returnEntity.ReturnNumber}. Reason: {reason.Trim()}");

            return Result.Ok(true);
        }, cancellationToken);
    }
}
