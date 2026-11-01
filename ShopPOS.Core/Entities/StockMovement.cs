using ShopPOS.Core.Common;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents an entry in the immutable inventory ledger.
/// Stock levels are always calculated by aggregating movements.
/// </summary>
public class StockMovement : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key to the affected Product.
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Navigation property to Product.
    /// </summary>
    public Product? Product { get; set; }

    /// <summary>
    /// Gets or sets the type of inventory movement.
    /// </summary>
    public StockMovementType MovementType { get; set; }

    /// <summary>
    /// Gets or sets the signed quantity: positive for incoming stock, negative for outgoing stock.
    /// Stored in SQLite as thousandths (x1000) via value converter to support fractional weights.
    /// </summary>
    public decimal Quantity { get; set; } = 0m;

    /// <summary>
    /// Gets or sets the unit cost at the time of movement.
    /// Stored in SQLite as minor units (x100) via value converter.
    /// </summary>
    public decimal UnitCost { get; set; } = 0m;

    /// <summary>
    /// Gets or sets the source entity type (e.g. "SaleInvoice", "PurchaseOrder", "Manual").
    /// </summary>
    public string? ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the foreign record ID in the referenced entity table.
    /// </summary>
    public int? ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets an optional explanatory note or reason for the movement.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the stock movement occurred.
    /// </summary>
    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
}

