using FluentValidation;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

public class SaleReturnService : ISaleReturnService
{
    private readonly ISaleReturnRepository _returnRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly IRepository<StockMovement> _movementRepository;
    private readonly IStockLedgerQuery _stockLedgerQuery;
    private readonly IShopSettingService _settingService;
    private readonly INumberSequenceService _numberSequenceService;
    private readonly ITransactionRunner _transactionRunner;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ISaleCalculator _calculator;
    private readonly IValidator<CreateSaleReturnDto> _returnValidator;

    public SaleReturnService(
        ISaleReturnRepository returnRepository,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        IRepository<StockMovement> movementRepository,
        IStockLedgerQuery stockLedgerQuery,
        IShopSettingService settingService,
        INumberSequenceService numberSequenceService,
        ITransactionRunner transactionRunner,
        IAuditLogService auditLogService,
        ICurrentUserProvider currentUserProvider,
        ISaleCalculator? calculator = null,
        IValidator<CreateSaleReturnDto>? returnValidator = null)
    {
        _returnRepository = returnRepository;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _movementRepository = movementRepository;
        _stockLedgerQuery = stockLedgerQuery;
        _settingService = settingService;
        _numberSequenceService = numberSequenceService;
        _transactionRunner = transactionRunner;
        _auditLogService = auditLogService;
        _currentUserProvider = currentUserProvider;
        _calculator = calculator ?? new SaleCalculator();
        _returnValidator = returnValidator ?? new ShopPOS.Core.Validators.CreateSaleReturnValidator();
    }

    public async Task<Result<List<ReturnableLineDto>>> LoadReturnableLinesAsync(int saleId, CancellationToken cancellationToken = default)
    {
        var sale = await _saleRepository.GetByIdWithDetailsAsync(saleId, cancellationToken);
        if (sale == null)
        {
            return Result<List<ReturnableLineDto>>.Failure($"Sale with ID {saleId} was not found.");
        }

        if (sale.Status != SaleStatus.Completed)
        {
            return Result<List<ReturnableLineDto>>.Failure($"Sale '{sale.SaleNumber}' status is {sale.Status}. Only completed sales can be returned.");
        }

        var lines = new List<ReturnableLineDto>();

        foreach (var item in sale.Items)
        {
            decimal alreadyReturned = await _returnRepository.GetTotalReturnedQuantityForSaleItemAsync(item.Id, cancellationToken);
            decimal remaining = Math.Max(0m, item.Quantity - alreadyReturned);

            lines.Add(new ReturnableLineDto
            {
                SaleItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Sku = item.Sku,
                Barcode = item.Barcode,
                SoldQuantity = item.Quantity,
                AlreadyReturnedQuantity = alreadyReturned,
                RemainingQuantity = remaining,
                OriginalUnitPrice = item.UnitPrice,
                OriginalDiscountPercent = item.DiscountPercent,
                OriginalDiscountAmount = item.DiscountAmount,
                OriginalTaxPercent = item.TaxPercent,
                LineTotal = item.LineTotal
            });
        }

        return Result<List<ReturnableLineDto>>.Ok(lines);
    }

    public async Task<Result<SaleReturnDetailDto>> CreateSaleReturnAsync(CreateSaleReturnDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _returnValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<SaleReturnDetailDto>.Failure(validation.Errors.First().ErrorMessage);
        }

        var settings = await _settingService.GetSettingsAsync();

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            Sale? originalSale = null;

            if (dto.SaleId.HasValue && dto.SaleId.Value > 0)
            {
                originalSale = await _saleRepository.GetByIdWithDetailsAsync(dto.SaleId.Value, cancellationToken);
                if (originalSale == null)
                {
                    return Result<SaleReturnDetailDto>.Failure($"Original sale with ID {dto.SaleId.Value} was not found.");
                }

                if (originalSale.Status != SaleStatus.Completed)
                {
                    return Result<SaleReturnDetailDto>.Failure($"Cannot return against sale '{originalSale.SaleNumber}' with status {originalSale.Status}.");
                }

                if (settings.SaleReturnWindowDays.HasValue && settings.SaleReturnWindowDays.Value > 0)
                {
                    var daysSinceSale = (DateTime.Now - originalSale.SaleDate).TotalDays;
                    if (daysSinceSale > settings.SaleReturnWindowDays.Value)
                    {
                        return Result<SaleReturnDetailDto>.Failure(
                            $"The return window of {settings.SaleReturnWindowDays.Value} day(s) has expired for sale '{originalSale.SaleNumber}' (completed {daysSinceSale:0.#} days ago, allowable return window of {settings.SaleReturnWindowDays.Value} days).");
                    }
                }
            }
            else
            {
                if (!settings.AllowReturnWithoutReceipt)
                {
                    return Result<SaleReturnDetailDto>.Failure(
                        "Returns without an original receipt/sale reference are not allowed in store settings (store policy).");
                }
            }

            var returnLines = new List<SaleReturnItem>();
            decimal totalSubTotal = 0m;
            decimal totalTax = 0m;
            decimal totalRefundAmount = 0m;

            foreach (var itemDto in dto.Items)
            {
                if (itemDto.Quantity <= 0m) continue;

                Product? product = await _productRepository.GetWithDetailsAsync(itemDto.ProductId, cancellationToken);
                if (product == null)
                {
                    return Result<SaleReturnDetailDto>.Failure($"Product with ID {itemDto.ProductId} was not found.");
                }

                decimal unitPrice;
                decimal taxPercent = 0m;
                decimal costPrice = product.CostPrice;

                if (originalSale != null && itemDto.SaleItemId.HasValue)
                {
                    var originalItem = originalSale.Items.FirstOrDefault(i => i.Id == itemDto.SaleItemId.Value);
                    if (originalItem == null)
                    {
                        return Result<SaleReturnDetailDto>.Failure($"Sale item ID {itemDto.SaleItemId.Value} does not belong to sale '{originalSale.SaleNumber}'.");
                    }

                    decimal alreadyReturned = await _returnRepository.GetTotalReturnedQuantityForSaleItemAsync(originalItem.Id, cancellationToken);
                    decimal availableToReturn = originalItem.Quantity - alreadyReturned;

                    if (itemDto.Quantity > availableToReturn)
                    {
                        return Result<SaleReturnDetailDto>.Failure(
                            $"Requested return quantity of {itemDto.Quantity} exceeds available returnable quantity ({availableToReturn}) for '{originalItem.ProductName}'. Maximum returnable quantity is {availableToReturn}.");
                    }

                    unitPrice = originalItem.UnitPrice;
                    taxPercent = originalItem.TaxPercent;
                    costPrice = originalItem.CostPriceSnapshot;
                }
                else
                {
                    unitPrice = itemDto.UnitPrice ?? product.SalePrice;
                    taxPercent = product.TaxPercent;
                }

                decimal lineGross = _calculator.Round(itemDto.Quantity * unitPrice);
                decimal lineTax = taxPercent > 0m ? _calculator.Round(lineGross * (taxPercent / 100m)) : 0m;
                decimal lineTotal = lineGross + lineTax;

                totalSubTotal += lineGross;
                totalTax += lineTax;
                totalRefundAmount += lineTotal;

                returnLines.Add(new SaleReturnItem
                {
                    SaleItemId = itemDto.SaleItemId,
                    ProductId = itemDto.ProductId,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Barcode = product.Barcode,
                    Quantity = itemDto.Quantity,
                    UnitPrice = unitPrice,
                    CostPriceSnapshot = costPrice,
                    TaxPercent = taxPercent,
                    LineTotal = lineTotal,
                    ReturnToStock = itemDto.ReturnToStock,
                    CreatedByUserId = _currentUserProvider.UserId
                });
            }

            if (returnLines.Count == 0)
            {
                return Result<SaleReturnDetailDto>.Failure("No valid return items were specified.");
            }

            var returnNumber = await _numberSequenceService.GetNextNumberAsync("SRET", cancellationToken);

            var saleReturn = new SaleReturn
            {
                ReturnNumber = returnNumber,
                ReturnDate = DateTime.Now,
                SaleId = dto.SaleId,
                Status = SaleReturnStatus.Posted,
                SubTotal = _calculator.Round(totalSubTotal),
                TaxAmount = _calculator.Round(totalTax),
                TotalAmount = _calculator.Round(totalRefundAmount),
                RefundMethod = dto.RefundMethod,
                RefundedAmount = _calculator.Round(totalRefundAmount),
                Reason = dto.Reason,
                CreatedByUserId = _currentUserProvider.UserId,
                Items = returnLines
            };

            await _returnRepository.AddAsync(saleReturn, cancellationToken);

            // Create restock movements for tracked items returned to stock
            foreach (var item in returnLines)
            {
                if (item.ReturnToStock)
                {
                    var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
                    if (product != null && product.TrackStock)
                    {
                        var movement = new StockMovement
                        {
                            ProductId = item.ProductId,
                            MovementType = StockMovementType.SaleReturn,
                            Quantity = item.Quantity,
                            UnitCost = item.CostPriceSnapshot,
                            ReferenceType = "SaleReturn",
                            ReferenceId = saleReturn.Id,
                            Note = $"Return {returnNumber} ({saleReturn.Reason ?? "Customer Return"})",
                            MovementDate = DateTime.Now,
                            CreatedByUserId = _currentUserProvider.UserId
                        };

                        await _movementRepository.AddAsync(movement, cancellationToken);
                    }
                }
            }

            await _auditLogService.LogAsync(
                userId: _currentUserProvider.UserId,
                action: "CreateSaleReturn",
                entityName: nameof(SaleReturn),
                entityId: saleReturn.Id,
                details: $"Return {returnNumber} posted for {_calculator.Round(totalRefundAmount):F2} via {dto.RefundMethod}.");

            var resultDto = new SaleReturnDetailDto
            {
                Id = saleReturn.Id,
                ReturnNumber = saleReturn.ReturnNumber,
                ReturnDate = saleReturn.ReturnDate,
                SaleId = saleReturn.SaleId,
                SaleNumber = originalSale?.SaleNumber,
                Status = saleReturn.Status,
                SubTotal = saleReturn.SubTotal,
                TaxAmount = saleReturn.TaxAmount,
                TotalAmount = saleReturn.TotalAmount,
                RefundMethod = saleReturn.RefundMethod,
                RefundedAmount = saleReturn.RefundedAmount,
                Reason = saleReturn.Reason,
                Items = saleReturn.Items.Select(i => new SaleReturnItemDetailDto
                {
                    Id = i.Id,
                    SaleItemId = i.SaleItemId,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Sku = i.Sku,
                    Barcode = i.Barcode,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    CostPriceSnapshot = i.CostPriceSnapshot,
                    TaxPercent = i.TaxPercent,
                    LineTotal = i.LineTotal,
                    ReturnToStock = i.ReturnToStock
                }).ToList()
            };

            return Result<SaleReturnDetailDto>.Ok(resultDto);
        }, cancellationToken);
    }

    public async Task<Result<bool>> CancelSaleReturnAsync(int returnId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<bool>.Failure("A cancellation reason is required.");
        }

        var saleReturn = await _returnRepository.GetByIdWithDetailsAsync(returnId, cancellationToken);
        if (saleReturn == null)
        {
            return Result<bool>.Failure($"Sale return with ID {returnId} was not found.");
        }

        if (saleReturn.Status != SaleReturnStatus.Posted)
        {
            return Result<bool>.Failure($"Only posted sale returns can be cancelled. Status is {saleReturn.Status}.");
        }

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            var settings = await _settingService.GetSettingsAsync();

            // Reverse any restock movements that were created
            foreach (var item in saleReturn.Items)
            {
                if (item.ReturnToStock)
                {
                    var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
                    if (product != null && product.TrackStock)
                    {
                        if (!settings.AllowNegativeStock)
                        {
                            var curStock = await _stockLedgerQuery.GetCurrentStockAsync(item.ProductId, cancellationToken);
                            if (curStock - item.Quantity < 0m)
                            {
                                return Result<bool>.Failure($"Cancelling return {saleReturn.ReturnNumber} would make stock negative for product '{product.Name}'. Current stock is {curStock}, reversing quantity is {item.Quantity}.");
                            }
                        }

                        var reversingMovement = new StockMovement
                        {
                            ProductId = item.ProductId,
                            MovementType = StockMovementType.Sale,
                            Quantity = -item.Quantity,
                            UnitCost = item.CostPriceSnapshot,
                            ReferenceType = "SaleReturnCancel",
                            ReferenceId = saleReturn.Id,
                            Note = $"Cancellation of return {saleReturn.ReturnNumber}: {reason.Trim()}",
                            MovementDate = DateTime.Now,
                            CreatedByUserId = _currentUserProvider.UserId
                        };

                        await _movementRepository.AddAsync(reversingMovement, cancellationToken);
                    }
                }
            }

            saleReturn.Status = SaleReturnStatus.Cancelled;
            saleReturn.CancelReason = reason.Trim();
            saleReturn.UpdatedAt = DateTime.UtcNow;

            await _returnRepository.UpdateAsync(saleReturn, cancellationToken);

            await _auditLogService.LogAsync(
                userId: _currentUserProvider.UserId,
                action: "CancelSaleReturn",
                entityName: nameof(SaleReturn),
                entityId: saleReturn.Id,
                details: $"Return {saleReturn.ReturnNumber} cancelled. Reason: {reason.Trim()}.");

            return Result<bool>.Ok(true);
        }, cancellationToken);
    }

    public async Task<Result<SaleReturnDetailDto>> GetSaleReturnByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var r = await _returnRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (r == null)
        {
            return Result<SaleReturnDetailDto>.Failure($"Sale return with ID {id} was not found.");
        }

        var dto = new SaleReturnDetailDto
        {
            Id = r.Id,
            ReturnNumber = r.ReturnNumber,
            ReturnDate = r.ReturnDate,
            SaleId = r.SaleId,
            SaleNumber = r.Sale?.SaleNumber,
            Status = r.Status,
            SubTotal = r.SubTotal,
            TaxAmount = r.TaxAmount,
            TotalAmount = r.TotalAmount,
            RefundMethod = r.RefundMethod,
            RefundedAmount = r.RefundedAmount,
            Reason = r.Reason,
            CancelReason = r.CancelReason,
            Items = r.Items.Select(i => new SaleReturnItemDetailDto
            {
                Id = i.Id,
                SaleItemId = i.SaleItemId,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Sku = i.Sku,
                Barcode = i.Barcode,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                CostPriceSnapshot = i.CostPriceSnapshot,
                TaxPercent = i.TaxPercent,
                LineTotal = i.LineTotal,
                ReturnToStock = i.ReturnToStock
            }).ToList()
        };

        return Result<SaleReturnDetailDto>.Ok(dto);
    }

    public async Task<PagedResult<SaleReturnDetailDto>> SearchSaleReturnsAsync(SaleReturnSearchDto criteria, CancellationToken cancellationToken = default)
    {
        return await _returnRepository.SearchSaleReturnsAsync(criteria, cancellationToken);
    }

    public async Task<BulkOperationResult> BulkCancelSaleReturnsAsync(IEnumerable<int> ids, string reason, int? userId, CancellationToken cancellationToken = default)
    {
        var result = new BulkOperationResult { TotalRequested = ids.Count() };
        foreach (var id in ids)
        {
            var r = await _returnRepository.GetByIdAsync(id, cancellationToken);
            if (r == null)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = $"ID {id}", Success = false, Message = "Sale return not found." });
                result.SkippedCount++;
                continue;
            }

            var cancelRes = await CancelSaleReturnAsync(id, reason, cancellationToken);
            if (cancelRes.Success)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = r.ReturnNumber, Success = true });
                result.SuccessCount++;
            }
            else
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = r.ReturnNumber, Success = false, Message = cancelRes.ErrorMessage ?? "Cannot cancel sale return." });
                result.SkippedCount++;
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<int>> GetFilteredSaleReturnIdsAsync(SaleReturnSearchDto criteria, CancellationToken cancellationToken = default)
    {
        return await _returnRepository.GetFilteredIdsAsync(criteria, cancellationToken);
    }

}
