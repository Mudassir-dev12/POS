using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a return of sold goods with refund adjustment.
/// </summary>
public class SaleReturn : BaseEntity
{
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; } = DateTime.Now;

    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }

    public SaleReturnStatus Status { get; set; } = SaleReturnStatus.Posted;

    public decimal SubTotal { get; set; } = 0m;
    public decimal TaxAmount { get; set; } = 0m;
    public decimal TotalAmount { get; set; } = 0m;

    public PaymentMethod RefundMethod { get; set; } = PaymentMethod.Cash;
    public decimal RefundedAmount { get; set; } = 0m;

    public string? Reason { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<SaleReturnItem> Items { get; set; } = new List<SaleReturnItem>();
}
