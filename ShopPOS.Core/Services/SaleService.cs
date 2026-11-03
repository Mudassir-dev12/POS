using FluentValidation;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

public class SaleService : ISaleService
{
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
    private readonly IValidator<SaveSaleDto> _saveValidator;
    private readonly IValidator<CompleteSaleDto> _completeValidator;

    public SaleService(
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
        IValidator<SaveSaleDto>? saveValidator = null,
        IValidator<CompleteSaleDto>? completeValidator = null)
    {
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
        _saveValidator = saveValidator ?? new ShopPOS.Core.Validators.SaveSaleValidator();
        _completeValidator = completeValidator ?? new ShopPOS.Core.Validators.CompleteSaleValidator();
    }

    public async Task<Result<SaleDetailDto>> SaveHeldBillAsync(SaveSaleDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _saveValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<SaleDetailDto>.Failure(validation.Errors.First().ErrorMessage);
        }

        var settings = await _settingService.GetSettingsAsync();

        // Calculate line inputs
        var lineInputs = dto.Items.Select(i => new SaleLineInput
        {
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            DiscountPercent = i.DiscountPercent,
            DiscountAmount = i.DiscountAmount,
            TaxPercent = i.TaxPercent
        }).ToList();

        var calc = _calculator.CalculateSale(
            lineInputs,
            dto.BillDiscountPercent,
            dto.BillDiscountAmount,
            settings.PricesIncludeTax,
            settings.CashRoundingStep,
            0m);

        Sale? sale = null;
        if (dto.Id.HasValue && dto.Id.Value > 0)
        {
            sale = await _saleRepository.GetByIdWithDetailsAsync(dto.Id.Value, cancellationToken);
            if (sale != null && sale.Status != SaleStatus.Held)
            {
                return Result<SaleDetailDto>.Failure($"Only held bills can be updated via SaveHeldBill. Status is {sale.Status}.");
            }
        }

        string holdNumber;
        if (sale == null)
        {
            holdNumber = await _numberSequenceService.GetNextNumberAsync("HOLD", cancellationToken);
            sale = new Sale
            {
                HoldNumber = holdNumber,
                SaleDate = DateTime.Now,
                Status = SaleStatus.Held,
                CreatedByUserId = _currentUserProvider.UserId
            };
        }
        else
        {
            holdNumber = sale.HoldNumber ?? await _numberSequenceService.GetNextNumberAsync("HOLD", cancellationToken);
            sale.HoldNumber = holdNumber;
            sale.Items.Clear();
        }

        sale.PriceLevelId = dto.PriceLevelId;
        sale.Notes = dto.Notes;
        sale.SubTotal = calc.SubTotal;
        sale.LineDiscountTotal = calc.LineDiscountTotal;
        sale.BillDiscountAmount = calc.BillDiscountAmount;
        sale.TaxAmount = calc.TaxAmount;
        sale.RoundOff = calc.RoundOff;
        sale.GrandTotal = calc.GrandTotal;
        sale.DueAmount = calc.GrandTotal;
        sale.PaidAmount = 0m;
        sale.ChangeAmount = 0m;

        int lineNo = 1;
        for (int i = 0; i < dto.Items.Count; i++)
        {
            var itemInput = dto.Items[i];
            var lineCalc = calc.Lines[i];
            var product = await _productRepository.GetWithDetailsAsync(itemInput.ProductId, cancellationToken);

            sale.Items.Add(new SaleItem
            {
                ProductId = itemInput.ProductId,
                ProductName = product?.Name ?? itemInput.ProductName ?? "Item",
                Sku = product?.Sku ?? itemInput.Sku ?? string.Empty,
                Barcode = product?.Barcode ?? itemInput.Barcode,
                Quantity = itemInput.Quantity,
                UnitPrice = itemInput.UnitPrice,
                CostPriceSnapshot = product?.CostPrice ?? 0m,
                DiscountPercent = itemInput.DiscountPercent,
                DiscountAmount = lineCalc.LineDiscountAmount,
                TaxPercent = itemInput.TaxPercent,
                LineTotal = lineCalc.LineTotal,
                Note = itemInput.Note,
                LineNo = lineNo++,
                CreatedByUserId = _currentUserProvider.UserId
            });
        }

        if (sale.Id == 0)
        {
            await _saleRepository.AddAsync(sale, cancellationToken);
        }
        else
        {
            sale.UpdatedAt = DateTime.UtcNow;
            await _saleRepository.UpdateAsync(sale, cancellationToken);
        }

        await _auditLogService.LogAsync(
            userId: _currentUserProvider.UserId,
            action: sale.Id == 0 ? "CreateHeldBill" : "UpdateHeldBill",
            entityName: nameof(Sale),
            entityId: sale.Id,
            details: $"Held bill {holdNumber} saved with {sale.Items.Count} item(s). Grand total: {sale.GrandTotal:F2}");

        return await GetSaleByIdAsync(sale.Id, cancellationToken);
    }

    public async Task<Result<List<SaleDetailDto>>> ListHeldBillsAsync(CancellationToken cancellationToken = default)
    {
        var heldBills = await _saleRepository.GetHeldBillsWithDetailsAsync(cancellationToken);
        var dtos = heldBills.Select(MapToDetailDto).ToList();
        return Result<List<SaleDetailDto>>.Ok(dtos);
    }

    public async Task<Result<bool>> DeleteHeldBillAsync(int id, CancellationToken cancellationToken = default)
    {
        var sale = await _saleRepository.GetByIdAsync(id, cancellationToken);
        if (sale == null)
        {
            return Result<bool>.Failure($"Held bill with ID {id} was not found.");
        }

        if (sale.Status != SaleStatus.Held)
        {
            return Result<bool>.Failure($"Cannot delete sale with status {sale.Status}. Only held bills can be deleted.");
        }

        sale.IsDeleted = true;
        sale.UpdatedAt = DateTime.UtcNow;
        await _saleRepository.UpdateAsync(sale, cancellationToken);

        await _auditLogService.LogAsync(
            userId: _currentUserProvider.UserId,
            action: "DeleteHeldBill",
            entityName: nameof(Sale),
            entityId: sale.Id,
            details: $"Held bill {sale.HoldNumber} was deleted.");

        return Result<bool>.Ok(true);
    }

    public async Task<Result<SaleDetailDto>> CompleteSaleAsync(CompleteSaleDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _completeValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<SaleDetailDto>.Failure(validation.Errors.First().ErrorMessage);
        }

        var settings = await _settingService.GetSettingsAsync();

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            // 1. Consume held bill if this sale originated from one
            if (dto.HeldBillId.HasValue && dto.HeldBillId.Value > 0)
            {
                var heldBill = await _saleRepository.GetByIdAsync(dto.HeldBillId.Value, cancellationToken);
                if (heldBill != null && heldBill.Status == SaleStatus.Held)
                {
                    heldBill.IsDeleted = true;
                    heldBill.UpdatedAt = DateTime.UtcNow;
                    await _saleRepository.UpdateAsync(heldBill, cancellationToken);
                }
            }

            // 2. Load & validate products
            var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
            var productList = await _productRepository.GetByIdsWithDetailsAsync(productIds, cancellationToken);
            var products = productList.ToDictionary(p => p.Id);

            foreach (var item in dto.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var product) || !product.IsActive)
                {
                    return Result<SaleDetailDto>.Failure($"Product with ID {item.ProductId} was not found or is inactive.");
                }

                // Decimal quantity rule
                if (!product.AllowDecimalQuantity && item.Quantity % 1m != 0m)
                {
                    return Result<SaleDetailDto>.Failure($"Product '{product.Name}' does not allow fractional quantities. Quantity must be a whole number.");
                }

                // Minimum price rule
                if (!settings.AllowSellingBelowMinPrice && product.MinSalePrice.HasValue && item.UnitPrice < product.MinSalePrice.Value)
                {
                    return Result<SaleDetailDto>.Failure(
                        $"Selling price of {item.UnitPrice:F2} for '{product.Name}' is below the minimum allowed price of {product.MinSalePrice.Value:F2}.");
                }
            }

            // 3. Ledger Stock Check inside the same transaction
            foreach (var item in dto.Items)
            {
                var product = products[item.ProductId];
                if (product.TrackStock)
                {
                    var currentStock = await _stockLedgerQuery.GetCurrentStockAsync(product.Id, cancellationToken);
                    if (!settings.AllowNegativeStock && currentStock - item.Quantity < 0m)
                    {
                        return Result<SaleDetailDto>.Failure(
                            $"Insufficient stock for '{product.Name}'. Available: {currentStock}, Requested: {item.Quantity}.");
                    }
                }
            }

            // 4. Server-side totals calculation via SaleCalculator
            decimal rawTotalPaid = dto.Payments.Sum(p => p.Amount);
            var lineInputs = dto.Items.Select(i => new SaleLineInput
            {
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountPercent = i.DiscountPercent,
                DiscountAmount = i.DiscountAmount,
                TaxPercent = i.TaxPercent
            }).ToList();

            var calc = _calculator.CalculateSale(
                lineInputs,
                dto.BillDiscountPercent,
                dto.BillDiscountAmount,
                settings.PricesIncludeTax,
                settings.CashRoundingStep,
                rawTotalPaid);

            // 5. Full payment rule: all sales must be paid in full (walk-in counter sales)
            if (rawTotalPaid < calc.GrandTotal)
            {
                return Result<SaleDetailDto>.Failure(
                    $"Payment total ({rawTotalPaid:F2}) is less than the grand total ({calc.GrandTotal:F2}). All sales must be paid in full at completion; credit sales are not permitted.");
            }

            // 6. Payment & Change rules
            decimal changeAmount = 0m;
            decimal effectivePaidAmount = calc.GrandTotal;
            var paymentEntities = new List<SalePayment>();

            if (rawTotalPaid > calc.GrandTotal)
            {
                // Overpayment allowed only on Cash
                var cashPayment = dto.Payments.FirstOrDefault(p => p.Method == PaymentMethod.Cash);
                if (cashPayment == null)
                {
                    return Result<SaleDetailDto>.Failure("Overpayment is only permitted on Cash transactions. Card/Bank payments cannot exceed the grand total.");
                }

                decimal excess = rawTotalPaid - calc.GrandTotal;
                changeAmount = excess;

                foreach (var p in dto.Payments)
                {
                    if (p.Method == PaymentMethod.Cash)
                    {
                        decimal tendered = p.TenderedAmount ?? p.Amount;
                        decimal applied = p.Amount - excess;
                        paymentEntities.Add(new SalePayment
                        {
                            Method = PaymentMethod.Cash,
                            Amount = applied,
                            TenderedAmount = tendered,
                            Reference = p.Reference,
                            PaidAt = DateTime.Now,
                            CreatedByUserId = _currentUserProvider.UserId
                        });
                    }
                    else
                    {
                        paymentEntities.Add(new SalePayment
                        {
                            Method = p.Method,
                            Amount = p.Amount,
                            TenderedAmount = p.Amount,
                            Reference = p.Reference,
                            PaidAt = DateTime.Now,
                            CreatedByUserId = _currentUserProvider.UserId
                        });
                    }
                }
            }
            else
            {
                foreach (var p in dto.Payments)
                {
                    paymentEntities.Add(new SalePayment
                    {
                        Method = p.Method,
                        Amount = p.Amount,
                        TenderedAmount = p.TenderedAmount ?? p.Amount,
                        Reference = p.Reference,
                        PaidAt = DateTime.Now,
                        CreatedByUserId = _currentUserProvider.UserId
                    });
                }
            }

            // 7. Number sequence assignment
            var saleNumber = await _numberSequenceService.GetNextNumberAsync("INV", cancellationToken);

            var sale = new Sale
            {
                SaleNumber = saleNumber,
                SaleDate = DateTime.Now,
                PriceLevelId = dto.PriceLevelId,
                Status = SaleStatus.Completed,
                SubTotal = calc.SubTotal,
                LineDiscountTotal = calc.LineDiscountTotal,
                BillDiscountAmount = calc.BillDiscountAmount,
                TaxAmount = calc.TaxAmount,
                RoundOff = calc.RoundOff,
                GrandTotal = calc.GrandTotal,
                PaidAmount = effectivePaidAmount,
                ChangeAmount = changeAmount,
                DueAmount = 0m,
                Notes = dto.Notes,
                CompletedAt = DateTime.Now,
                CreatedByUserId = _currentUserProvider.UserId,
                Payments = paymentEntities
            };

            int lineNo = 1;
            decimal totalCost = 0m;

            for (int i = 0; i < dto.Items.Count; i++)
            {
                var itemInput = dto.Items[i];
                var lineCalc = calc.Lines[i];
                var product = products[itemInput.ProductId];

                decimal lineCost = product.CostPrice * itemInput.Quantity;
                totalCost += lineCost;

                sale.Items.Add(new SaleItem
                {
                    ProductId = itemInput.ProductId,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Barcode = product.Barcode,
                    Quantity = itemInput.Quantity,
                    UnitPrice = itemInput.UnitPrice,
                    CostPriceSnapshot = product.CostPrice,
                    DiscountPercent = itemInput.DiscountPercent,
                    DiscountAmount = lineCalc.LineDiscountAmount,
                    TaxPercent = itemInput.TaxPercent,
                    LineTotal = lineCalc.LineTotal,
                    Note = itemInput.Note,
                    LineNo = lineNo++,
                    CreatedByUserId = _currentUserProvider.UserId
                });
            }

            sale.TotalCost = _calculator.Round(totalCost);

            await _saleRepository.AddAsync(sale, cancellationToken);

            // 8. Write StockMovements for tracked products
            foreach (var item in sale.Items)
            {
                var product = products[item.ProductId];
                if (product.TrackStock)
                {
                    var movement = new StockMovement
                    {
                        ProductId = item.ProductId,
                        MovementType = StockMovementType.Sale,
                        Quantity = -item.Quantity,
                        UnitCost = item.CostPriceSnapshot,
                        ReferenceType = "Sale",
                        ReferenceId = sale.Id,
                        Note = $"Sale {saleNumber}",
                        MovementDate = DateTime.Now,
                        CreatedByUserId = _currentUserProvider.UserId
                    };

                    await _movementRepository.AddAsync(movement, cancellationToken);
                }
            }

            // 9. Audit log
            await _auditLogService.LogAsync(
                userId: _currentUserProvider.UserId,
                action: "CompleteSale",
                entityName: nameof(Sale),
                entityId: sale.Id,
                details: $"Sale {saleNumber} completed. Total: {sale.GrandTotal:F2}, Paid: {sale.PaidAmount:F2}, Change: {sale.ChangeAmount:F2}.");

            return await GetSaleByIdAsync(sale.Id, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Result<bool>> CancelSaleAsync(int saleId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<bool>.Failure("A cancellation reason is required.");
        }

        var sale = await _saleRepository.GetByIdWithDetailsAsync(saleId, cancellationToken);
        if (sale == null)
        {
            return Result<bool>.Failure($"Sale with ID {saleId} was not found.");
        }

        if (sale.Status != SaleStatus.Completed)
        {
            return Result<bool>.Failure($"Only completed sales can be cancelled. Sale '{sale.SaleNumber}' status is {sale.Status}.");
        }

        // Must not have posted returns
        if (sale.Returns.Any(r => r.Status == SaleReturnStatus.Posted))
        {
            return Result<bool>.Failure($"Sale '{sale.SaleNumber}' cannot be cancelled because it has posted returns. Use a Sale Return instead.");
        }

        // Allowed only if dated today
        if (sale.SaleDate.Date != DateTime.Today)
        {
            return Result<bool>.Failure(
                $"Sale '{sale.SaleNumber}' cannot be cancelled because it was completed on {sale.SaleDate:yyyy-MM-dd}. Sales dated before today cannot be cancelled; use a Sale Return instead.");
        }

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            // Reverse stock movements
            foreach (var item in sale.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
                if (product != null && product.TrackStock)
                {
                    var reversingMovement = new StockMovement
                    {
                        ProductId = item.ProductId,
                        MovementType = StockMovementType.SaleCancel,
                        Quantity = item.Quantity,
                        UnitCost = item.CostPriceSnapshot,
                        ReferenceType = "Sale",
                        ReferenceId = sale.Id,
                        Note = $"Cancellation of sale {sale.SaleNumber}: {reason.Trim()}",
                        MovementDate = DateTime.Now,
                        CreatedByUserId = _currentUserProvider.UserId
                    };

                    await _movementRepository.AddAsync(reversingMovement, cancellationToken);
                }
            }

            sale.Status = SaleStatus.Cancelled;
            sale.CancelledAt = DateTime.Now;
            sale.CancelReason = reason.Trim();
            sale.UpdatedAt = DateTime.UtcNow;

            await _saleRepository.UpdateAsync(sale, cancellationToken);

            await _auditLogService.LogAsync(
                userId: _currentUserProvider.UserId,
                action: "CancelSale",
                entityName: nameof(Sale),
                entityId: sale.Id,
                details: $"Sale {sale.SaleNumber} cancelled. Reason: {reason.Trim()}.");

            return Result<bool>.Ok(true);
        }, cancellationToken);
    }

    public async Task<Result<SaleDetailDto>> GetSaleByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var sale = await _saleRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (sale == null)
        {
            return Result<SaleDetailDto>.Failure($"Sale with ID {id} was not found.");
        }

        return Result<SaleDetailDto>.Ok(MapToDetailDto(sale));
    }

    public async Task<PagedResult<SaleSummaryDto>> SearchSalesAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default)
    {
        return await _saleRepository.SearchSalesAsync(criteria, cancellationToken);
    }

    public async Task<Result<FilteredSalesPaymentBreakdownDto>> GetFilteredSalesPaymentBreakdownAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default)
    {
        var breakdown = await _saleRepository.GetFilteredPaymentBreakdownAsync(criteria, cancellationToken);
        return Result<FilteredSalesPaymentBreakdownDto>.Ok(breakdown);
    }

    public async Task<Result<TodaySalesSummaryDto>> GetTodaySummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Local);
        var endOfDay = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59, DateTimeKind.Local);

        var summary = await _saleRepository.GetTodaySummaryAsync(startOfDay, endOfDay, cancellationToken);
        return Result<TodaySalesSummaryDto>.Ok(summary);
    }

    private static SaleDetailDto MapToDetailDto(Sale sale)
    {
        return new SaleDetailDto
        {
            Id = sale.Id,
            SaleNumber = sale.SaleNumber,
            HoldNumber = sale.HoldNumber,
            SaleDate = sale.SaleDate,
            PriceLevelId = sale.PriceLevelId,
            PriceLevelName = sale.PriceLevel?.Name,
            Status = sale.Status,
            SubTotal = sale.SubTotal,
            LineDiscountTotal = sale.LineDiscountTotal,
            BillDiscountAmount = sale.BillDiscountAmount,
            TaxAmount = sale.TaxAmount,
            RoundOff = sale.RoundOff,
            GrandTotal = sale.GrandTotal,
            PaidAmount = sale.PaidAmount,
            ChangeAmount = sale.ChangeAmount,
            DueAmount = sale.DueAmount,
            TotalCost = sale.TotalCost,
            Notes = sale.Notes,
            CompletedAt = sale.CompletedAt,
            CancelledAt = sale.CancelledAt,
            CancelReason = sale.CancelReason,
            IsOlderThan7Days = (DateTime.Now - sale.SaleDate).TotalDays > 7,
            Items = sale.Items.Select(i => new SaleItemDetailDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Sku = i.Sku,
                Barcode = i.Barcode,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                CostPriceSnapshot = i.CostPriceSnapshot,
                DiscountPercent = i.DiscountPercent,
                DiscountAmount = i.DiscountAmount,
                TaxPercent = i.TaxPercent,
                LineTotal = i.LineTotal,
                Note = i.Note,
                LineNo = i.LineNo
            }).ToList(),
            Payments = sale.Payments.Select(p => new SalePaymentDetailDto
            {
                Id = p.Id,
                Method = p.Method,
                Amount = p.Amount,
                TenderedAmount = p.TenderedAmount,
                Reference = p.Reference,
                PaidAt = p.PaidAt
            }).ToList()
        };
    }

    public async Task<BulkOperationResult> BulkDeleteHeldBillsAsync(IEnumerable<int> ids, int? userId, CancellationToken cancellationToken = default)
    {
        var result = new BulkOperationResult { TotalRequested = ids.Count() };
        foreach (var id in ids)
        {
            var sale = await _saleRepository.GetByIdAsync(id, cancellationToken);
            if (sale == null)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = $"ID {id}", Success = false, Message = "Sale not found." });
                result.SkippedCount++;
                continue;
            }
            if (sale.Status != SaleStatus.Held)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = sale.SaleNumber, Success = false, Message = "Cannot delete completed sale. Cancel instead." });
                result.SkippedCount++;
                continue;
            }

            var delRes = await DeleteHeldBillAsync(id, cancellationToken);
            if (delRes.Success)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = sale.SaleNumber, Success = true });
                result.SuccessCount++;
            }
            else
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = sale.SaleNumber, Success = false, Message = delRes.ErrorMessage ?? "Delete failed." });
                result.SkippedCount++;
            }
        }
        return result;
    }

    public async Task<BulkOperationResult> BulkCancelSalesAsync(IEnumerable<int> ids, string reason, int? userId, CancellationToken cancellationToken = default)
    {
        var result = new BulkOperationResult { TotalRequested = ids.Count() };
        foreach (var id in ids)
        {
            var sale = await _saleRepository.GetByIdAsync(id, cancellationToken);
            if (sale == null)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = $"ID {id}", Success = false, Message = "Sale not found." });
                result.SkippedCount++;
                continue;
            }

            var cancelRes = await CancelSaleAsync(id, reason, cancellationToken);
            if (cancelRes.Success)
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = sale.SaleNumber, Success = true });
                result.SuccessCount++;
            }
            else
            {
                result.Items.Add(new BulkItemResult { Id = id, Identifier = sale.SaleNumber, Success = false, Message = cancelRes.ErrorMessage ?? "Cannot cancel sale." });
                result.SkippedCount++;
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<int>> GetFilteredSaleIdsAsync(SaleSearchDto criteria, CancellationToken cancellationToken = default)
    {
        return await _saleRepository.GetFilteredIdsAsync(criteria, cancellationToken);
    }

}
