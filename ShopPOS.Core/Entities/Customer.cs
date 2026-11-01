using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a retail customer or account client.
/// </summary>
public class Customer : BaseEntity
{
    /// <summary>
    /// Gets or sets the full name or business name of the customer.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the contact phone number.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the customer email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the customer tax / VAT identification number.
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Gets or sets the customer address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets internal notes for the customer.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowable credit balance.
    /// Stored in SQLite as minor units (x100) via value converter.
    /// </summary>
    public decimal CreditLimit { get; set; } = 0m;

    /// <summary>
    /// Gets or sets the initial balance for the customer (positive: they owe us / receivable; negative: we owe them).
    /// Stored in SQLite as minor units (x100) via value converter.
    /// </summary>
    public decimal OpeningBalance { get; set; } = 0m;

    /// <summary>
    /// Gets or sets whether this customer profile is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the optional default pricing tier for this customer.
    /// </summary>
    public int? DefaultPriceLevelId { get; set; }

    /// <summary>
    /// Navigation property to Default Price Level.
    /// </summary>
    public PriceLevel? DefaultPriceLevel { get; set; }

    /// <summary>
    /// Navigation collection of sales associated with this customer.
    /// </summary>
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();

    /// <summary>
    /// Navigation collection of sale returns associated with this customer.
    /// </summary>
    public ICollection<SaleReturn> SaleReturns { get; set; } = new List<SaleReturn>();
}
