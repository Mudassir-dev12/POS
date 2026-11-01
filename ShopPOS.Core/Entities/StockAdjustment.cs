using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

public class StockAdjustment : BaseEntity
{
    public string AdjustmentNumber { get; set; } = string.Empty;
    public DateTime AdjustmentDate { get; set; } = DateTime.Today;
    public StockAdjustmentReason Reason { get; set; } = StockAdjustmentReason.CountCorrection;
    public string? Notes { get; set; }
    public StockAdjustmentStatus Status { get; set; } = StockAdjustmentStatus.Draft;

    public DateTime? PostedAt { get; set; }
    public int? PostedByUserId { get; set; }
    public User? PostedByUser { get; set; }

    public DateTime? CancelledAt { get; set; }
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<StockAdjustmentItem> Items { get; set; } = new List<StockAdjustmentItem>();
}

