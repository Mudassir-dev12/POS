using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a category for business operating expenses.
/// </summary>
public class ExpenseCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

