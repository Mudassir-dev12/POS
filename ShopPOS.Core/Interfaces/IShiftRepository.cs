using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public interface IShiftRepository : IRepository<Shift>
{
    Task<Shift?> GetOpenShiftAsync(CancellationToken cancellationToken = default);
    Task<Shift?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<ShiftSummaryDto> GetShiftSummaryAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<Shift>> SearchShiftsAsync(ShiftSearchDto criteria, CancellationToken cancellationToken = default);
    Task<CashMovement> AddCashMovementAsync(CashMovement movement, CancellationToken cancellationToken = default);
}

