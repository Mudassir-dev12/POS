using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IShiftService
{
    Task<Result<Shift>> OpenShiftAsync(OpenShiftDto dto, CancellationToken cancellationToken = default);
    Task<Result<ShiftSummaryDto>> CloseShiftAsync(CloseShiftDto dto, CancellationToken cancellationToken = default);
    Task<Result<Shift?>> GetCurrentOpenShiftAsync(CancellationToken cancellationToken = default);
    Task<Result<CashMovement>> AddCashMovementAsync(CashMovementDto dto, CancellationToken cancellationToken = default);
    Task<Result<ShiftSummaryDto>> GetShiftSummaryAsync(int shiftId, CancellationToken cancellationToken = default);
    Task<PagedResult<Shift>> SearchShiftsAsync(ShiftSearchDto criteria, CancellationToken cancellationToken = default);
}

