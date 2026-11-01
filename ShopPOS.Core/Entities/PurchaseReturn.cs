using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a debit note / return of purchased goods back to a vendor.
/// </summary>
public class PurchaseReturn : BaseEntity
{
    public string ReturnNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public DateTime ReturnDate { get; set; } = DateTime.Today;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    public PurchaseReturnStatus Status { get; set; } = PurchaseReturnStatus.Posted;

    public DateTime? PostedAt { get; set; }
    public int? PostedByUserId { get; set; }
    public User? PostedByUser { get; set; }

    public DateTime? CancelledAt { get; set; }
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
}

