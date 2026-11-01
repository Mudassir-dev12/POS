using FluentValidation;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

public class ShiftService : IShiftService
{
    private readonly IShiftRepository _shiftRepository;
    private readonly INumberSequenceService _numberSequenceService;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IValidator<OpenShiftDto> _openShiftValidator;
    private readonly IValidator<CloseShiftDto> _closeShiftValidator;
    private readonly IValidator<CashMovementDto> _cashMovementValidator;

    public ShiftService(
        IShiftRepository shiftRepository,
        INumberSequenceService numberSequenceService,
        IAuditLogService auditLogService,
        ICurrentUserProvider currentUserProvider,
        IValidator<OpenShiftDto> openShiftValidator,
        IValidator<CloseShiftDto> closeShiftValidator,
        IValidator<CashMovementDto> cashMovementValidator)
    {
        _shiftRepository = shiftRepository;
        _numberSequenceService = numberSequenceService;
        _auditLogService = auditLogService;
        _currentUserProvider = currentUserProvider;
        _openShiftValidator = openShiftValidator;
        _closeShiftValidator = closeShiftValidator;
        _cashMovementValidator = cashMovementValidator;
    }

    public async Task<Result<Shift>> OpenShiftAsync(OpenShiftDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _openShiftValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<Shift>.Failure(validation.Errors.First().ErrorMessage);
        }

        var existingOpen = await _shiftRepository.GetOpenShiftAsync(cancellationToken);
        if (existingOpen != null)
        {
            return Result<Shift>.Failure($"Shift '{existingOpen.ShiftNumber}' is currently open. You must close the active shift before opening a new one.");
        }

        var shiftNumber = await _numberSequenceService.GetNextNumberAsync("SHF", cancellationToken);
        var shift = new Shift
        {
            ShiftNumber = shiftNumber,
            OpenedAt = DateTime.Now,
            OpeningCash = Math.Round(dto.OpeningCash, 2, MidpointRounding.AwayFromZero),
            Status = ShiftStatus.Open,
            Notes = dto.Notes,
            CreatedByUserId = _currentUserProvider.UserId
        };

        await _shiftRepository.AddAsync(shift, cancellationToken);

        await _auditLogService.LogAsync(
            userId: _currentUserProvider.UserId,
            action: "OpenShift",
            entityName: nameof(Shift),
            entityId: shift.Id,
            details: $"Shift {shiftNumber} opened with initial cash {shift.OpeningCash:F2}.");

        return Result<Shift>.Ok(shift);
    }

    public async Task<Result<ShiftSummaryDto>> CloseShiftAsync(CloseShiftDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _closeShiftValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ShiftSummaryDto>.Failure(validation.Errors.First().ErrorMessage);
        }

        var shift = await _shiftRepository.GetByIdWithDetailsAsync(dto.ShiftId, cancellationToken);
        if (shift == null)
        {
            return Result<ShiftSummaryDto>.Failure($"Shift with ID {dto.ShiftId} does not exist.");
        }

        if (shift.Status == ShiftStatus.Closed)
        {
            return Result<ShiftSummaryDto>.Failure($"Shift '{shift.ShiftNumber}' is already closed and immutable.");
        }

        var summary = await _shiftRepository.GetShiftSummaryAsync(shift.Id, cancellationToken);
        decimal counted = Math.Round(dto.CountedCash, 2, MidpointRounding.AwayFromZero);
        decimal diff = Math.Round(counted - summary.ExpectedCash, 2, MidpointRounding.AwayFromZero);

        shift.ClosedAt = DateTime.Now;
        shift.CountedCash = counted;
        shift.ExpectedCash = summary.ExpectedCash;
        shift.Difference = diff;
        shift.Status = ShiftStatus.Closed;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            shift.Notes = string.IsNullOrWhiteSpace(shift.Notes) ? dto.Notes : $"{shift.Notes} | {dto.Notes}";
        }
        shift.UpdatedAt = DateTime.UtcNow;

        await _shiftRepository.UpdateAsync(shift, cancellationToken);

        await _auditLogService.LogAsync(
            userId: _currentUserProvider.UserId,
            action: "CloseShift",
            entityName: nameof(Shift),
            entityId: shift.Id,
            details: $"Shift {shift.ShiftNumber} closed. Expected: {summary.ExpectedCash:F2}, Counted: {counted:F2}, Difference: {diff:F2}.");

        summary.ClosedAt = shift.ClosedAt;
        summary.CountedCash = counted;
        summary.Difference = diff;
        summary.Status = ShiftStatus.Closed;

        return Result<ShiftSummaryDto>.Ok(summary);
    }

    public async Task<Result<Shift?>> GetCurrentOpenShiftAsync(CancellationToken cancellationToken = default)
    {
        var openShift = await _shiftRepository.GetOpenShiftAsync(cancellationToken);
        return Result<Shift?>.Ok(openShift);
    }

    public async Task<Result<CashMovement>> AddCashMovementAsync(CashMovementDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _cashMovementValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CashMovement>.Failure(validation.Errors.First().ErrorMessage);
        }

        var shift = await _shiftRepository.GetByIdAsync(dto.ShiftId, cancellationToken);
        if (shift == null)
        {
            return Result<CashMovement>.Failure($"Shift with ID {dto.ShiftId} does not exist.");
        }

        if (shift.Status == ShiftStatus.Closed)
        {
            return Result<CashMovement>.Failure($"Cannot record cash movements against a closed shift ('{shift.ShiftNumber}').");
        }

        var movement = new CashMovement
        {
            ShiftId = dto.ShiftId,
            Type = dto.Type,
            Amount = Math.Round(dto.Amount, 2, MidpointRounding.AwayFromZero),
            Reason = dto.Reason.Trim(),
            MovedAt = DateTime.Now,
            CreatedByUserId = _currentUserProvider.UserId
        };

        var created = await _shiftRepository.AddCashMovementAsync(movement, cancellationToken);

        await _auditLogService.LogAsync(
            userId: _currentUserProvider.UserId,
            action: $"CashMovement_{dto.Type}",
            entityName: nameof(CashMovement),
            entityId: created.Id,
            details: $"Shift {shift.ShiftNumber}: {dto.Type} of {movement.Amount:F2}. Reason: {movement.Reason}");

        return Result<CashMovement>.Ok(created);
    }

    public async Task<Result<ShiftSummaryDto>> GetShiftSummaryAsync(int shiftId, CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await _shiftRepository.GetShiftSummaryAsync(shiftId, cancellationToken);
            return Result<ShiftSummaryDto>.Ok(summary);
        }
        catch (KeyNotFoundException ex)
        {
            return Result<ShiftSummaryDto>.Failure(ex.Message);
        }
    }

    public async Task<PagedResult<Shift>> SearchShiftsAsync(ShiftSearchDto criteria, CancellationToken cancellationToken = default)
    {
        return await _shiftRepository.SearchShiftsAsync(criteria, cancellationToken);
    }
}
