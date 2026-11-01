namespace ShopPOS.Core.Common;

/// <summary>
/// Base class for all domain entities providing auditing and soft-delete capabilities.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Gets or sets the primary key identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the UTC last updated timestamp.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user ID who created this record, if known.
    /// </summary>
    public int? CreatedByUserId { get; set; }

    /// <summary>
    /// Gets or sets whether this record is soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}

