using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents an operating expense entry.
/// </summary>
public class Expense : BaseEntity
{
    public string ExpenseNumber { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    public int ExpenseCategoryId { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string? Payee { get; set; }
    public string? Notes { get; set; }
    public bool IsCancelled { get; set; } = false;
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }
}

