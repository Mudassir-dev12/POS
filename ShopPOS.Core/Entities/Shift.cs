using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a cash register work shift. Only one shift may be open at any time.
/// </summary>
public class Shift : BaseEntity
{
    public string ShiftNumber { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public decimal OpeningCash { get; set; } = 0m;
    public DateTime? ClosedAt { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? Difference { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public string? Notes { get; set; }

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<CashMovement> CashMovements { get; set; } = new List<CashMovement>();
}

