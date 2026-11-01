using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a payment made to a supplier.
/// Payments are immutable once recorded. Mistakes are corrected by reversing/cancelling records.
/// </summary>
public class SupplierPayment : BaseEntity
{
    public string PaymentNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>
    /// Optional foreign key to a specific purchase invoice.
    /// If null, this is an unallocated payment against the supplier's overall balance.
    /// </summary>
    public int? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    public bool IsCancelled { get; set; } = false;
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
}

