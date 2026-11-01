using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Records user and system actions for accountability and forensic auditing.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>
    /// Gets or sets the ID of the user who performed the action, if applicable.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Navigation property to the User.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Gets or sets the action verb (e.g. "LoginSuccess", "PasswordChanged", "CreatedProduct").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target entity class name (e.g. "User", "Product").
    /// </summary>
    public string? EntityName { get; set; }

    /// <summary>
    /// Gets or sets the target entity record ID.
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// Gets or sets detailed descriptions, serialized changes, or context.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the event timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

