using FluentValidation;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

public class StockAdjustmentService : IStockAdjustmentService
{
    private readonly IStockAdjustmentRepository _adjustmentRepo;
    private readonly IProductRepository _productRepo;
    private readonly IStockLedgerQuery _stockLedgerQuery;
    private readonly IShopSettingRepository _shopSettingRepo;
    private readonly INumberSequenceService _sequenceService;
    private readonly ITransactionRunner _transactionRunner;
    private readonly IAuditLogService _auditLogService;
    private readonly IValidator<CreateStockAdjustmentDto> _createValidator;
    private readonly IValidator<UpdateStockAdjustmentDto> _updateValidator;

    public StockAdjustmentService(
        IStockAdjustmentRepository adjustmentRepo,
        IProductRepository productRepo,
        IStockLedgerQuery stockLedgerQuery,
        IShopSettingRepository shopSettingRepo,
        INumberSequenceService sequenceService,
        ITransactionRunner transactionRunner,
        IAuditLogService auditLogService,
        IValidator<CreateStockAdjustmentDto> createValidator,
        IValidator<UpdateStockAdjustmentDto> updateValidator)
    {
        _adjustmentRepo = adjustmentRepo;
        _productRepo = productRepo;
        _stockLedgerQuery = stockLedgerQuery;
        _shopSettingRepo = shopSettingRepo;
        _sequenceService = sequenceService;
        _transactionRunner = transactionRunner;
        _auditLogService = auditLogService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<Result<PagedResult<StockAdjustmentListDto>>> GetAdjustmentsAsync(DateTime? fromDate, DateTime? toDate, StockAdjustmentStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var result = await _adjustmentRepo.GetAdjustmentsAsync(fromDate, toDate, status, pageNumber, pageSize, cancellationToken);
        return Result<PagedResult<StockAdjustmentListDto>>.Ok(result);
    }

    public async Task<Result<StockAdjustmentDetailDto>> GetAdjustmentByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var detail = await _adjustmentRepo.GetAdjustmentDetailAsync(id, cancellationToken);
        if (detail == null)
        {
            return Result<StockAdjustmentDetailDto>.Failure("Stock Adjustment not found.");
        }

        return Result<StockAdjustmentDetailDto>.Ok(detail);
    }

    public async Task<Result<StockAdjustmentDetailDto>> CreateAdjustmentDraftAsync(CreateStockAdjustmentDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var validationResult = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<StockAdjustmentDetailDto>.Failure(string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage)));
        }

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var stockMap = await _stockLedgerQuery.GetStockBatchAsync(productIds, cancellationToken);

        var adjNumber = await _sequenceService.GetNextNumberAsync("ADJ", cancellationToken);
        var adjustmentDate = dto.AdjustmentDate.TimeOfDay == TimeSpan.Zero ? dto.AdjustmentDate.Date.Add(DateTime.Now.TimeOfDay) : dto.AdjustmentDate;
        var adjustment = new StockAdjustment
        {
            AdjustmentNumber = adjNumber,
            AdjustmentDate = adjustmentDate,
            Reason = dto.Reason,
            Notes = dto.Notes,
            Status = StockAdjustmentStatus.Draft,
            CreatedByUserId = userId
        };

        foreach (var itemDto in dto.Items)
        {
            var product = await _productRepo.GetByIdAsync(itemDto.ProductId, cancellationToken);
            if (product == null)
            {
                return Result<StockAdjustmentDetailDto>.Failure($"Product with ID {itemDto.ProductId} not found.");
            }

            decimal currentStock = stockMap.TryGetValue(itemDto.ProductId, out var s) ? s : 0m;
            decimal diff = itemDto.NewQuantity - currentStock;

            adjustment.Items.Add(new StockAdjustmentItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                SystemQuantity = currentStock,
                NewQuantity = itemDto.NewQuantity,
                Difference = diff,
                UnitCost = product.CostPrice,
                Note = itemDto.Note,
                CreatedByUserId = userId
            });
        }

        await _adjustmentRepo.CreateAdjustmentAsync(adjustment, cancellationToken);
        await _auditLogService.LogAsync(userId, "Create", "StockAdjustment", adjustment.Id, $"Created stock adjustment draft {adjustment.AdjustmentNumber} with {adjustment.Items.Count} items.");

        var detail = await _adjustmentRepo.GetAdjustmentDetailAsync(adjustment.Id, cancellationToken);
        return Result<StockAdjustmentDetailDto>.Ok(detail!);
    }

    public async Task<Result<StockAdjustmentDetailDto>> UpdateAdjustmentDraftAsync(UpdateStockAdjustmentDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var validationResult = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<StockAdjustmentDetailDto>.Failure(string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage)));
        }

        var adjustment = await _adjustmentRepo.GetAdjustmentByIdAsync(dto.Id, includeItems: true, cancellationToken);
        if (adjustment == null)
        {
            return Result<StockAdjustmentDetailDto>.Failure("Stock Adjustment not found.");
        }

        if (adjustment.Status != StockAdjustmentStatus.Draft)
        {
            return Result<StockAdjustmentDetailDto>.Failure("Only Draft adjustments can be edited.");
        }

        adjustment.AdjustmentDate = dto.AdjustmentDate.TimeOfDay == TimeSpan.Zero ? dto.AdjustmentDate.Date.Add(DateTime.Now.TimeOfDay) : dto.AdjustmentDate;
        adjustment.Reason = dto.Reason;
        adjustment.Notes = dto.Notes;
        adjustment.UpdatedAt = DateTime.UtcNow;

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var stockMap = await _stockLedgerQuery.GetStockBatchAsync(productIds, cancellationToken);

        // Mark existing items as deleted
        foreach (var item in adjustment.Items)
        {
            item.IsDeleted = true;
        }

        // Add new items
        foreach (var itemDto in dto.Items)
        {
            var product = await _productRepo.GetByIdAsync(itemDto.ProductId, cancellationToken);
            if (product == null)
            {
                return Result<StockAdjustmentDetailDto>.Failure($"Product with ID {itemDto.ProductId} not found.");
            }

            decimal currentStock = stockMap.TryGetValue(itemDto.ProductId, out var s) ? s : 0m;
            decimal diff = itemDto.NewQuantity - currentStock;

            adjustment.Items.Add(new StockAdjustmentItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                SystemQuantity = currentStock,
                NewQuantity = itemDto.NewQuantity,
                Difference = diff,
                UnitCost = product.CostPrice,
                Note = itemDto.Note,
                CreatedByUserId = userId
            });
        }

        await _adjustmentRepo.UpdateAdjustmentAsync(adjustment, cancellationToken);
        await _auditLogService.LogAsync(userId, "Update", "StockAdjustment", adjustment.Id, $"Updated stock adjustment draft {adjustment.AdjustmentNumber}.");

        var detail = await _adjustmentRepo.GetAdjustmentDetailAsync(adjustment.Id, cancellationToken);
        return Result<StockAdjustmentDetailDto>.Ok(detail!);
    }

    public async Task<Result<bool>> DeleteAdjustmentDraftAsync(int id, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var adjustment = await _adjustmentRepo.GetAdjustmentByIdAsync(id, includeItems: false, cancellationToken);
        if (adjustment == null)
        {
            return Result<bool>.Failure("Stock Adjustment not found.");
        }

        if (adjustment.Status != StockAdjustmentStatus.Draft)
        {
            return Result<bool>.Failure("Only Draft adjustments can be deleted.");
        }

        await _adjustmentRepo.SoftDeleteAdjustmentAsync(id, cancellationToken);
        await _auditLogService.LogAsync(userId, "Delete", "StockAdjustment", id, $"Deleted stock adjustment draft {adjustment.AdjustmentNumber}.");
        return Result<bool>.Ok(true);
    }

    public async Task<Result<StockAdjustmentDetailDto>> PostAdjustmentAsync(int id, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        try
        {
            StockAdjustmentDetailDto? resultDetail = null;

            await _transactionRunner.RunInTransactionAsync(async () =>
            {
                var adjustment = await _adjustmentRepo.GetAdjustmentByIdAsync(id, includeItems: true, cancellationToken);
                if (adjustment == null)
                {
                    throw new InvalidOperationException("Stock Adjustment not found.");
                }

                if (adjustment.Status != StockAdjustmentStatus.Draft)
                {
                    throw new InvalidOperationException($"Cannot post adjustment with status '{adjustment.Status}'. Only Draft adjustments can be posted.");
                }

                var settings = await _shopSettingRepo.GetCurrentSettingsAsync(cancellationToken);
                bool allowNegative = settings?.AllowNegativeStock ?? false;

                var activeItems = adjustment.Items.Where(i => !i.IsDeleted).ToList();
                if (activeItems.Count == 0)
                {
                    throw new InvalidOperationException("Cannot post adjustment without any line items.");
                }

                foreach (var item in activeItems)
                {
                    // Re-read ledger stock inside the transaction for atomic accuracy
                    decimal currentStock = await _stockLedgerQuery.GetCurrentStockAsync(item.ProductId, cancellationToken);
                    decimal diff = item.NewQuantity - currentStock;

                    if (!allowNegative && item.NewQuantity < 0)
                    {
                        throw new InvalidOperationException($"Negative stock is not permitted for product: {item.ProductName}");
                    }

                    item.SystemQuantity = currentStock;
                    item.Difference = diff;

                    if (diff != 0)
                    {
                        var movement = new StockMovement
                        {
                            ProductId = item.ProductId,
                            MovementType = StockMovementType.Adjustment,
                            Quantity = diff,
                            UnitCost = item.UnitCost,
                            ReferenceType = "StockAdjustment",
                            ReferenceId = adjustment.Id,
                            MovementDate = adjustment.AdjustmentDate.TimeOfDay == TimeSpan.Zero ? adjustment.AdjustmentDate.Date.Add(DateTime.Now.TimeOfDay) : adjustment.AdjustmentDate,
                            CreatedByUserId = userId,
                            Note = $"Stock Adjustment {adjustment.AdjustmentNumber}: {(string.IsNullOrWhiteSpace(item.Note) ? adjustment.Reason.ToString() : item.Note)}"
                        };

                        await _adjustmentRepo.AddStockMovementAsync(movement, cancellationToken);
                    }
                }

                adjustment.Status = StockAdjustmentStatus.Posted;
                adjustment.PostedAt = DateTime.UtcNow;
                adjustment.PostedByUserId = userId;

                await _adjustmentRepo.SaveChangesAsync(cancellationToken);
                await _auditLogService.LogAsync(userId, "Post", "StockAdjustment", adjustment.Id, $"Posted stock adjustment {adjustment.AdjustmentNumber} with {activeItems.Count} items.");
            }, cancellationToken);

            resultDetail = await _adjustmentRepo.GetAdjustmentDetailAsync(id, cancellationToken);
            return Result<StockAdjustmentDetailDto>.Ok(resultDetail!);
        }
        catch (Exception ex)
        {
            return Result<StockAdjustmentDetailDto>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> CancelAdjustmentAsync(int id, string reason, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<bool>.Failure("A cancellation reason is required.");
        }

        try
        {
            await _transactionRunner.RunInTransactionAsync(async () =>
            {
                var adjustment = await _adjustmentRepo.GetAdjustmentByIdAsync(id, includeItems: true, cancellationToken);
                if (adjustment == null)
                {
                    throw new InvalidOperationException("Stock Adjustment not found.");
                }

                if (adjustment.Status != StockAdjustmentStatus.Posted)
                {
                    throw new InvalidOperationException($"Cannot cancel adjustment with status '{adjustment.Status}'. Only Posted adjustments can be cancelled.");
                }

                var settings = await _shopSettingRepo.GetCurrentSettingsAsync(cancellationToken);
                bool allowNegative = settings?.AllowNegativeStock ?? false;

                var activeItems = adjustment.Items.Where(i => !i.IsDeleted).ToList();

                // Validate that reversal will not drive stock negative if not permitted
                if (!allowNegative)
                {
                    foreach (var item in activeItems.Where(i => i.Difference > 0))
                    {
                        decimal currentStock = await _stockLedgerQuery.GetCurrentStockAsync(item.ProductId, cancellationToken);
                        if (currentStock - item.Difference < 0)
                        {
                            throw new InvalidOperationException($"Cannot cancel adjustment because reversing it would result in negative stock for product '{item.ProductName}' (Current: {currentStock}, Reversal: -{item.Difference}).");
                        }
                    }
                }

                // Create reversing movements
                foreach (var item in activeItems.Where(i => i.Difference != 0))
                {
                    var revMovement = new StockMovement
                    {
                        ProductId = item.ProductId,
                        MovementType = StockMovementType.AdjustmentCancel,
                        Quantity = -item.Difference,
                        UnitCost = item.UnitCost,
                        ReferenceType = "StockAdjustmentCancel",
                        ReferenceId = adjustment.Id,
                        MovementDate = DateTime.Now,
                        CreatedByUserId = userId,
                        Note = $"Cancel Adjustment {adjustment.AdjustmentNumber}: {reason.Trim()}"
                    };

                    await _adjustmentRepo.AddStockMovementAsync(revMovement, cancellationToken);
                }

                adjustment.Status = StockAdjustmentStatus.Cancelled;
                adjustment.CancelledAt = DateTime.UtcNow;
                adjustment.CancelledByUserId = userId;
                adjustment.CancelReason = reason.Trim();

                await _adjustmentRepo.SaveChangesAsync(cancellationToken);
                await _auditLogService.LogAsync(userId, "Cancel", "StockAdjustment", adjustment.Id, $"Cancelled stock adjustment {adjustment.AdjustmentNumber}. Reason: {reason.Trim()}");
            }, cancellationToken);

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> CreateAndPostSingleAdjustmentAsync(int productId, decimal targetQuantity, string reasonText, StockAdjustmentReason reasonType, int? userId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        if (string.IsNullOrWhiteSpace(reasonText))
        {
            return Result<bool>.Failure("Adjustment reason is required.");
        }

        var product = await _productRepo.GetByIdAsync(productId, cancellationToken);
        if (product == null)
        {
            return Result<bool>.Failure("Product not found.");
        }

        var adjNumber = await _sequenceService.GetNextNumberAsync("ADJ", cancellationToken);
        decimal currentStock = await _stockLedgerQuery.GetCurrentStockAsync(productId, cancellationToken);

        var adjustment = new StockAdjustment
        {
            AdjustmentNumber = adjNumber,
            AdjustmentDate = DateTime.Today,
            Reason = reasonType,
            Notes = reasonText.Trim(),
            Status = StockAdjustmentStatus.Draft,
            CreatedByUserId = userId
        };

        adjustment.Items.Add(new StockAdjustmentItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Sku = product.Sku,
            SystemQuantity = currentStock,
            NewQuantity = targetQuantity,
            Difference = targetQuantity - currentStock,
            UnitCost = product.CostPrice,
            Note = reasonText.Trim(),
            CreatedByUserId = userId
        });

        await _adjustmentRepo.CreateAdjustmentAsync(adjustment, cancellationToken);

        var postResult = await PostAdjustmentAsync(adjustment.Id, userId, cancellationToken);
        if (!postResult.Success)
        {
            return Result<bool>.Failure(postResult.ErrorMessage ?? "Failed to post single stock adjustment.");
        }

        return Result<bool>.Ok(true);
    }

    public async Task<BulkOperationResult> BulkDeleteDraftsAsync(IEnumerable<int> ids, int? userId, CancellationToken cancellationToken = default)
    {
        var result = new BulkOperationResult { TotalRequested = ids.Count() };
        foreach (var id in ids)
        {
            var adj = await _adjustmentRepo.GetAdjustmentByIdAsync(id, includeItems: false, cancellationToken);
            if (adj == null)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = $"ID {id}", Success = false, Message = "Adjustment not found." });
                result.SkippedCount++;
                continue;
            }
            if (adj.Status != StockAdjustmentStatus.Draft)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = adj.AdjustmentNumber, Success = false, Message = "Cannot delete posted or cancelled adjustment. Cancel instead." });
                result.SkippedCount++;
                continue;
            }

            var delRes = await DeleteAdjustmentDraftAsync(id, userId, cancellationToken);
            if (delRes.Success)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = adj.AdjustmentNumber, Success = true });
                result.SuccessCount++;
            }
            else
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = adj.AdjustmentNumber, Success = false, Message = delRes.ErrorMessage ?? "Delete failed." });
                result.SkippedCount++;
            }
        }
        return result;
    }

    public async Task<BulkOperationResult> BulkCancelAdjustmentsAsync(IEnumerable<int> ids, string reason, int? userId, CancellationToken cancellationToken = default)
    {
        var result = new BulkOperationResult { TotalRequested = ids.Count() };
        foreach (var id in ids)
        {
            var adj = await _adjustmentRepo.GetAdjustmentByIdAsync(id, includeItems: false, cancellationToken);
            if (adj == null)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = $"ID {id}", Success = false, Message = "Adjustment not found." });
                result.SkippedCount++;
                continue;
            }

            var cancelRes = await CancelAdjustmentAsync(id, reason, userId, cancellationToken);
            if (cancelRes.Success)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = adj.AdjustmentNumber, Success = true });
                result.SuccessCount++;
            }
            else
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = adj.AdjustmentNumber, Success = false, Message = cancelRes.ErrorMessage ?? "Cannot cancel adjustment." });
                result.SkippedCount++;
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<int>> GetFilteredAdjustmentIdsAsync(DateTime? fromDate, DateTime? toDate, StockAdjustmentStatus? status, CancellationToken cancellationToken = default)
    {
        return await _adjustmentRepo.GetFilteredIdsAsync(fromDate, toDate, status, cancellationToken);
    }

}
