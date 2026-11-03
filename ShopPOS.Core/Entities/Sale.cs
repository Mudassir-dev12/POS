using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a point-of-sale sales transaction or held bill.
/// </summary>
public class Sale : BaseEntity
{
    public string? SaleNumber { get; set; }
    public string? HoldNumber { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.Now;

    public int? PriceLevelId { get; set; }
    public PriceLevel? PriceLevel { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public decimal SubTotal { get; set; } = 0m;
    public decimal LineDiscountTotal { get; set; } = 0m;
    public decimal BillDiscountAmount { get; set; } = 0m;
    public decimal TaxAmount { get; set; } = 0m;
    public decimal RoundOff { get; set; } = 0m;
    public decimal GrandTotal { get; set; } = 0m;
    public decimal PaidAmount { get; set; } = 0m;
    public decimal ChangeAmount { get; set; } = 0m;

    /// <summary>
    /// Historical due balance for pre-migration sales. New sales are strictly paid in full (DueAmount = 0).
    /// </summary>
    public decimal DueAmount { get; set; } = 0m;
    public decimal TotalCost { get; set; } = 0m;

    public string? Notes { get; set; }

    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<SalePayment> Payments { get; set; } = new List<SalePayment>();
    public ICollection<SaleReturn> Returns { get; set; } = new List<SaleReturn>();
}
