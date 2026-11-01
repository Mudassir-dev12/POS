using FluentValidation;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

public class SupplierPaymentService : ISupplierPaymentService
{
    private readonly ITransactionRunner _transactionRunner;
    private readonly ISupplierPaymentRepository _paymentRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly IPurchaseRepository _purchaseRepo;
    private readonly INumberSequenceService _sequenceService;
    private readonly IAuditLogService _auditLogService;
    private readonly IPermissionService _permissionService;
    private readonly IValidator<CreateSupplierPaymentDto> _validator;

    public SupplierPaymentService(
        ITransactionRunner transactionRunner,
        ISupplierPaymentRepository paymentRepo,
        ISupplierRepository supplierRepo,
        IPurchaseRepository purchaseRepo,
        INumberSequenceService sequenceService,
        IAuditLogService auditLogService,
        IPermissionService permissionService,
        IValidator<CreateSupplierPaymentDto> validator)
    {
        _transactionRunner = transactionRunner;
        _paymentRepo = paymentRepo;
        _supplierRepo = supplierRepo;
        _purchaseRepo = purchaseRepo;
        _sequenceService = sequenceService;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
        _validator = validator;
    }

    public async Task<Result<PagedResult<SupplierPaymentDto>>> GetPaymentsAsync(int? supplierId, int pageNumber, int pageSize, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManagePurchases(userRole))
        {
            return Result.Failure<PagedResult<SupplierPaymentDto>>("Permission denied. You do not have access to Supplier Payments.");
        }

        var result = await _paymentRepo.GetPaymentsAsync(supplierId, pageNumber, pageSize, cancellationToken);
        return Result.Ok(result);
    }

    public async Task<Result<SupplierPaymentDto>> RecordPaymentAsync(CreateSupplierPaymentDto dto, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManagePurchases(userRole))
        {
            return Result.Failure<SupplierPaymentDto>("Permission denied. Only Admins and Managers can record payments.");
        }

        var val = await _validator.ValidateAsync(dto, cancellationToken);
        if (!val.IsValid)
        {
            return Result.Invalid<SupplierPaymentDto>(val.ToDictionary(), "Validation failed for supplier payment.");
        }

        var supplier = await _supplierRepo.GetByIdAsync(dto.SupplierId, cancellationToken);
        if (supplier == null || !supplier.IsActive)
        {
            return Result.Failure<SupplierPaymentDto>("Selected supplier is invalid or inactive.");
        }

        Purchase? purchase = null;
        if (dto.PurchaseId.HasValue && dto.PurchaseId.Value > 0)
        {
            purchase = await _purchaseRepo.GetPurchaseByIdAsync(dto.PurchaseId.Value, false, cancellationToken);
            if (purchase == null)
            {
                return Result.Failure<SupplierPaymentDto>($"Purchase ID {dto.PurchaseId.Value} was not found.");
            }

            if (purchase.Status != PurchaseStatus.Posted)
            {
                return Result.Failure<SupplierPaymentDto>($"Cannot allocate payment to a purchase with status '{purchase.Status}'. Payments can only be made against Posted purchases.");
            }

            if (purchase.SupplierId != dto.SupplierId)
            {
                return Result.Failure<SupplierPaymentDto>("Selected purchase belongs to a different supplier.");
            }

            decimal totalPaid = await _paymentRepo.GetTotalPaidForPurchaseAsync(purchase.Id, cancellationToken);
            decimal totalReturned = await _purchaseRepo.GetTotalReturnedForPurchaseAsync(purchase.Id, cancellationToken);
            decimal balance = Math.Max(0m, purchase.GrandTotal - totalReturned - totalPaid);

            if (dto.Amount > balance)
            {
                return Result.Failure<SupplierPaymentDto>($"Payment amount ({dto.Amount:N2}) exceeds the outstanding balance on purchase {purchase.PurchaseNumber} ({balance:N2}).");
            }
        }

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            string paymentNumber = await _sequenceService.GetNextNumberAsync("SPAY", cancellationToken);

            var payment = new SupplierPayment
            {
                PaymentNumber = paymentNumber,
                SupplierId = dto.SupplierId,
                PurchaseId = dto.PurchaseId > 0 ? dto.PurchaseId : null,
                PaymentDate = dto.PaymentDate.TimeOfDay == TimeSpan.Zero ? dto.PaymentDate.Date.Add(DateTime.Now.TimeOfDay) : dto.PaymentDate,
                Amount = Math.Round(dto.Amount, 2, MidpointRounding.AwayFromZero),
                Method = dto.Method,
                Reference = dto.Reference?.Trim(),
                Notes = dto.Notes?.Trim(),
                CreatedByUserId = userId
            };

            await _paymentRepo.CreatePaymentAsync(payment, cancellationToken);

            if (purchase != null)
            {
                decimal currentPaid = await _paymentRepo.GetTotalPaidForPurchaseAsync(purchase.Id, cancellationToken);
                await _paymentRepo.UpdatePurchasePaidAmountAsync(purchase.Id, currentPaid, cancellationToken);
            }

            await _auditLogService.LogAsync(
                userId,
                "RecordSupplierPayment",
                "SupplierPayment",
                payment.Id,
                $"Recorded payment {payment.PaymentNumber} of {payment.Amount:N2} to supplier '{supplier.Name}'");

            var paymentDto = new SupplierPaymentDto
            {
                Id = payment.Id,
                PaymentNumber = payment.PaymentNumber,
                SupplierId = payment.SupplierId,
                SupplierName = supplier.Name,
                PurchaseId = payment.PurchaseId,
                PurchaseNumber = purchase?.PurchaseNumber,
                PaymentDate = payment.PaymentDate,
                Amount = payment.Amount,
                Method = payment.Method,
                Reference = payment.Reference,
                Notes = payment.Notes
            };

            return Result.Ok(paymentDto);
        }, cancellationToken);
    }

    public async Task<Result<bool>> CancelPaymentAsync(int paymentId, string reason, int? userId, string? userRole, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.CanManagePurchases(userRole))
        {
            return Result.Failure<bool>("Permission denied. Only Admins and Managers can cancel payments.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<bool>("A reason is required to cancel a payment.");
        }

        return await _transactionRunner.RunInTransactionAsync(async () =>
        {
            var payment = await _paymentRepo.GetPaymentByIdAsync(paymentId, cancellationToken);
            if (payment == null)
            {
                return Result.Failure<bool>($"Payment ID {paymentId} was not found.");
            }

            if (payment.IsCancelled)
            {
                return Result.Failure<bool>("This payment has already been cancelled.");
            }

            await _paymentRepo.CancelPaymentAsync(paymentId, reason.Trim(), userId ?? 0, cancellationToken);

            if (payment.PurchaseId.HasValue)
            {
                decimal remainingPaid = await _paymentRepo.GetTotalPaidForPurchaseAsync(payment.PurchaseId.Value, cancellationToken);
                await _paymentRepo.UpdatePurchasePaidAmountAsync(payment.PurchaseId.Value, remainingPaid, cancellationToken);
            }

            await _auditLogService.LogAsync(
                userId,
                "CancelSupplierPayment",
                "SupplierPayment",
                payment.Id,
                $"Cancelled payment {payment.PaymentNumber}. Reason: {reason.Trim()}");

            return Result.Ok(true);
        }, cancellationToken);
    }
}
