using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a payment tender applied toward a sale transaction.
/// </summary>
public class SalePayment : BaseEntity
{
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public decimal Amount { get; set; } = 0m;
    public decimal? TenderedAmount { get; set; }
    public string? Reference { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.Now;
}

