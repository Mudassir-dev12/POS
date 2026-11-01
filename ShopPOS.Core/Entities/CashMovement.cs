using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a non-sale cash transaction during a shift (e.g. expenses, drawer drops, owner withdrawals).
/// </summary>
public class CashMovement : BaseEntity
{
    public int ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; } = 0m;
    public string Reason { get; set; } = string.Empty;
    public DateTime MovedAt { get; set; } = DateTime.Now;
}

