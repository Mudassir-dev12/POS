using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a purchase order / incoming supplier invoice.
/// </summary>
public class Purchase : BaseEntity
{
    public string PurchaseNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string? SupplierInvoiceNo { get; set; }

    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public DateTime? DueDate { get; set; }

    public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime? PostedAt { get; set; }
    public int? PostedByUserId { get; set; }
    public User? PostedByUser { get; set; }

    public DateTime? CancelledAt { get; set; }
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
    public ICollection<SupplierPayment> Payments { get; set; } = new List<SupplierPayment>();
    public ICollection<PurchaseReturn> Returns { get; set; } = new List<PurchaseReturn>();
}

