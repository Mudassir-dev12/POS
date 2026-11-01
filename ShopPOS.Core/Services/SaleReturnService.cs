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
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<StockMovement> _movementRepository;
    private readonly IStockLedgerQuery _stockLedgerQuery;
    private readonly ICustomerBalanceQuery _customerBalanceQuery;
    private readonly IShopSettingService _settingService;
    private readonly IShiftRepository _shiftRepository;
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
        IRepository<Customer> customerRepository,
        IRepository<StockMovement> movementRepository,
        IStockLedgerQuery stockLedgerQuery,
        ICustomerBalanceQuery customerBalanceQuery,
        IShopSettingService settingService,
        IShiftRepository shiftRepository,
        INumberSequenceService numberSequenceService,
        ITransactionRunner transactionRunner,
        IAuditLogService auditLogService,
        ICurrentUserProvider currentUserProvider,
        ISaleCalculator calculator,
        IValidator<CreateSaleReturnDto> returnValidator)
    {
        _returnRepository = returnRepository;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _movementRepository = movementRepository;
        _stockLedgerQuery = stockLedgerQuery;
        _customerBalanceQuery = customerBalanceQuery;
        _settingService = settingService;
        _shiftRepository = shiftRepository;
        _numberSequenceService = numberSequenceService;
        _transactionRunner = transactionRunner;
        _auditLogService = auditLogService;
        _currentUserProvider = currentUserProvider;
        _calculator = calculator;
        _returnValidator = returnValidator;
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
            return Result<List<ReturnableLineDto>>.Failure($"Sale '{sale.SaleNumber}' cannot be returned because its status is {sale.Status}.");
        }

        var pastReturns = await _returnRepository.GetReturnsForSaleAsync(saleId, cancellationToken);
        var alreadyReturnedMap = pastReturns
            .Where(r => r.Status == SaleReturnStatus.Posted)
            .SelectMany(r => r.Items)
            .Where(i => i.SaleItemId.HasValue)
            .GroupBy(i => i.SaleItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var returnableLines = new List<ReturnableLineDto>();
        foreach (var item in sale.Items)
        {
            decimal alreadyReturned = alreadyReturnedMap.TryGetValue(item.Id, out var qty) ? qty : 0m;
            decimal remaining = Math.Max(0m, item.Quantity - alreadyReturned);

            returnableLines.Add(new ReturnableLineDto
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

        return Result<List<ReturnableLineDto>>.Ok(returnableLines);
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
            int? effectiveCustomerId = dto.CustomerId;

            if (dto.SaleId.HasValue)
            {
                originalSale = await _saleRepository.GetByIdWithDetailsAsync(dto.SaleId.Value, cancellationToken);
                if (originalSale == null)
                {
                    return Result<SaleReturnDetailDto>.Failure($"Referenced sale ID {dto.SaleId.Value} was not found.");
                }

                if (originalSale.Status != SaleStatus.Completed)
                {
                    return Result<SaleReturnDetailDto>.Failure($"Cannot process a return against a sale with status {originalSale.Status}.");
                }

                // Check SaleReturnWindowDays
                if (settings.SaleReturnWindowDays.HasValue && settings.SaleReturnWindowDays.Value > 0)
                {
                    var daysSinceSale = (DateTime.Now - originalSale.SaleDate).TotalDays;
                    if (daysSinceSale > settings.SaleReturnWindowDays.Value)
                    {
                        return Result<SaleReturnDetailDto>.Failure(
                            $"The allowable return window of {settings.SaleReturnWindowDays.Value} days for sale '{originalSale.SaleNumber}' has expired ({Math.Floor(daysSinceSale)} days since sale).");
                    }
                }

                effectiveCustomerId = originalSale.CustomerId;
            }
            else
            {
                // Return without receipt
                if (!settings.AllowReturnWithoutReceipt)
                {
                    return Result<SaleReturnDetailDto>.Failure("Returns without original receipt are not allowed in store settings.");
                }
            }

            // Resolve shift for cash refund accounting
            int? shiftId = dto.ShiftId;
            if (!shiftId.HasValue)
            {
                var openShift = await _shiftRepository.GetOpenShiftAsync(cancellationToken);
                shiftId = openShift?.Id;
            }

            var returnLines = new List<SaleReturnItem>();
            decimal totalSubTotal = 0m;
            decimal totalTax = 0m;
            decimal totalRefundAmount = 0m;

            var pastReturns = originalSale != null
                ? await _returnRepository.GetReturnsForSaleAsync(originalSale.Id, cancellationToken)
                : new List<SaleReturn>();

            var alreadyReturnedMap = pastReturns
                .Where(r => r.Status == SaleReturnStatus.Posted)
                .SelectMany(r => r.Items)
                .Where(i => i.SaleItemId.HasValue)
                .GroupBy(i => i.SaleItemId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            foreach (var input in dto.Items)
            {
                SaleItem? originalLine = null;
                Product? product = null;

                if (originalSale != null)
                {
                    if (input.SaleItemId.HasValue)
                    {
                        originalLine = originalSale.Items.FirstOrDefault(i => i.Id == input.SaleItemId.Value);
                    }
                    else
                    {
                        originalLine = originalSale.Items.FirstOrDefault(i => i.ProductId == input.ProductId);
                    }

                    if (originalLine == null)
                    {
                        return Result<SaleReturnDetailDto>.Failure($"Item with ProductId {input.ProductId} is not part of sale '{originalSale.SaleNumber}'.");
                    }

                    product = await _productRepository.GetWithDetailsAsync(originalLine.ProductId, cancellationToken);
                    if (product == null)
                    {
                        return Result<SaleReturnDetailDto>.Failure($"Product ID {originalLine.ProductId} does not exist.");
                    }

                    decimal alreadyReturned = alreadyReturnedMap.TryGetValue(originalLine.Id, out var prev) ? prev : 0m;
                    decimal returnable = originalLine.Quantity - alreadyReturned;

                    if (input.Quantity > returnable)
                    {
                        return Result<SaleReturnDetailDto>.Failure(
                            $"Cannot return {input.Quantity} of '{originalLine.ProductName}'. Maximum returnable quantity is {returnable}.");
                    }

                    // Calculate line refund using original pricing
                    var calcLine = _calculator.CalculateReturnLine(
                        quantity: input.Quantity,
                        originalUnitPrice: originalLine.UnitPrice,
                        originalDiscountPercent: originalLine.DiscountPercent,
                        originalDiscountAmount: originalLine.DiscountAmount,
                        originalSoldQuantity: originalLine.Quantity,
                        taxPercent: originalLine.TaxPercent,
                        pricesIncludeTax: settings.PricesIncludeTax);

                    totalSubTotal += calcLine.GrossAmount;
                    totalTax += calcLine.TaxAmount;
                    totalRefundAmount += calcLine.LineTotal;

                    returnLines.Add(new SaleReturnItem
                    {
                        SaleItemId = originalLine.Id,
                        ProductId = originalLine.ProductId,
                        ProductName = originalLine.ProductName,
                        Sku = originalLine.Sku,
                        Barcode = originalLine.Barcode,
                        Quantity = input.Quantity,
                        UnitPrice = originalLine.UnitPrice,
                        CostPriceSnapshot = originalLine.CostPriceSnapshot,
                        TaxPercent = originalLine.TaxPercent,
                        LineTotal = calcLine.LineTotal,
                        ReturnToStock = input.ReturnToStock,
                        CreatedByUserId = _currentUserProvider.UserId
                    });
                }
                else
                {
                    // Return without receipt
                    product = await _productRepository.GetWithDetailsAsync(input.ProductId, cancellationToken);
                    if (product == null || !product.IsActive)
                    {
                        return Result<SaleReturnDetailDto>.Failure($"Product with ID {input.ProductId} was not found or is inactive.");
                    }

                    decimal unitPrice = input.UnitPrice ?? product.SalePrice;
                    decimal gross = _calculator.Round(input.Quantity * unitPrice);
                    decimal taxRate = Math.Max(0m, product.TaxPercent) / 100m;
                    decimal taxAmount;
                    decimal lineTotal;

                    if (settings.PricesIncludeTax)
                    {
                        decimal netOfTax = taxRate > 0m ? _calculator.Round(gross / (1m + taxRate)) : gross;
                        taxAmount = gross - netOfTax;
                        lineTotal = gross;
                    }
                    else
                    {
                        taxAmount = taxRate > 0m ? _calculator.Round(gross * taxRate) : 0m;
                        lineTotal = gross + taxAmount;
                    }

                    totalSubTotal += gross;
                    totalTax += taxAmount;
                    totalRefundAmount += lineTotal;

                    returnLines.Add(new SaleReturnItem
                    {
                        SaleItemId = null,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Sku = product.Sku,
                        Barcode = product.Barcode,
                        Quantity = input.Quantity,
                        UnitPrice = unitPrice,
                        CostPriceSnapshot = product.CostPrice,
                        TaxPercent = product.TaxPercent,
                        LineTotal = lineTotal,
                        ReturnToStock = input.ReturnToStock,
                        CreatedByUserId = _currentUserProvider.UserId
                    });
                }
            }

            // Refund method validation
            var effectiveRefundMethod = dto.RefundMethod;
            if (effectiveRefundMethod == PaymentMethod.StoreCredit)
            {
                if (!effectiveCustomerId.HasValue)
                {
                    // StoreCredit requires a customer; otherwise convert to cash
                    effectiveRefundMethod = PaymentMethod.Cash;
                }
                else
                {
                    decimal currentBalance = await _customerBalanceQuery.GetCustomerBalanceAsync(effectiveCustomerId.Value, cancellationToken);
                    if (currentBalance <= 0m)
                    {
                        // Customer owes nothing; convert to cash refund
                        effectiveRefundMethod = PaymentMethod.Cash;
                    }
                }
            }

            var returnNumber = await _numberSequenceService.GetNextNumberAsync("SRET", cancellationToken);

            var saleReturn = new SaleReturn
            {
                ReturnNumber = returnNumber,
                ReturnDate = DateTime.Now,
                SaleId = dto.SaleId,
                CustomerId = effectiveCustomerId,
                ShiftId = shiftId,
                Status = SaleReturnStatus.Posted,
                SubTotal = _calculator.Round(totalSubTotal),
                TaxAmount = _calculator.Round(totalTax),
                TotalAmount = _calculator.Round(totalRefundAmount),
                RefundMethod = effectiveRefundMethod,
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
                details: $"Return {returnNumber} posted for {_calculator.Round(totalRefundAmount):F2} via {effectiveRefundMethod}.");

            var resultDto = new SaleReturnDetailDto
            {
                Id = saleReturn.Id,
                ReturnNumber = saleReturn.ReturnNumber,
                ReturnDate = saleReturn.ReturnDate,
                SaleId = saleReturn.SaleId,
                SaleNumber = originalSale?.SaleNumber,
                CustomerId = saleReturn.CustomerId,
                CustomerName = originalSale?.Customer?.Name,
                ShiftId = saleReturn.ShiftId,
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
            return Result<bool>.Failure($"Sale return with ID {returnId} does not exist.");
        }

        if (saleReturn.Status == SaleReturnStatus.Cancelled)
        {
            return Result<bool>.Failure($"Sale return '{saleReturn.ReturnNumber}' is already cancelled.");
        }

        var settings = await _settingService.GetSettingsAsync();

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            // If any item was restocked, check if taking that stock back away would cause negative stock
            foreach (var item in saleReturn.Items.Where(i => i.ReturnToStock))
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
                if (product != null && product.TrackStock && !settings.AllowNegativeStock)
                {
                    var currentStock = await _stockLedgerQuery.GetCurrentStockAsync(item.ProductId, cancellationToken);
                    if (currentStock - item.Quantity < 0m)
                    {
                        return Result<bool>.Failure(
                            $"Cannot cancel return '{saleReturn.ReturnNumber}' because reversing restock for '{item.ProductName}' would make stock negative. Current stock: {currentStock}, Required: {item.Quantity}.");
                    }
                }
            }

            // Write reversing movements
            foreach (var item in saleReturn.Items.Where(i => i.ReturnToStock))
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
                if (product != null && product.TrackStock)
                {
                    var reversingMovement = new StockMovement
                    {
                        ProductId = item.ProductId,
                        MovementType = StockMovementType.SaleReturnCancel,
                        Quantity = -item.Quantity,
                        UnitCost = item.CostPriceSnapshot,
                        ReferenceType = "SaleReturn",
                        ReferenceId = saleReturn.Id,
                        Note = $"Cancellation of return {saleReturn.ReturnNumber}: {reason.Trim()}",
                        MovementDate = DateTime.Now,
                        CreatedByUserId = _currentUserProvider.UserId
                    };

                    await _movementRepository.AddAsync(reversingMovement, cancellationToken);
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
                details: $"Sale return {saleReturn.ReturnNumber} cancelled. Reason: {reason.Trim()}.");

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
            CustomerId = r.CustomerId,
            CustomerName = r.Customer?.Name,
            ShiftId = r.ShiftId,
            ShiftNumber = r.Shift?.ShiftNumber,
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
}
