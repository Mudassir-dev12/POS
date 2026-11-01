using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a vendor or supplier from whom items are purchased.
/// </summary>
public class Supplier : BaseEntity
{
    /// <summary>
    /// Gets or sets the company or supplier name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the primary contact person.
    /// </summary>
    public string? ContactPerson { get; set; }

    /// <summary>
    /// Gets or sets the contact phone number.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the supplier email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the supplier tax or VAT identification number.
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Gets or sets the supplier address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets internal notes or payment terms.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the opening balance (positive: we owe them / payable; negative: they owe us).
    /// Stored in SQLite as minor units (x100) via value converter.
    /// </summary>
    public decimal OpeningBalance { get; set; } = 0m;

    /// <summary>
    /// Gets or sets whether this supplier account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public ICollection<SupplierPayment> Payments { get; set; } = new List<SupplierPayment>();
    public ICollection<PurchaseReturn> Returns { get; set; } = new List<PurchaseReturn>();
}
