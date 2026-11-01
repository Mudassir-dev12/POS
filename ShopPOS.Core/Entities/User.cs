using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents an application user account.
/// </summary>
public class User : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique username used for login.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the BCrypt password hash.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full display name of the user.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the associated role ID.
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// Navigation property for the assigned role.
    /// </summary>
    public Role? Role { get; set; }

    /// <summary>
    /// Gets or sets whether this user account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC timestamp of the last successful login.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Gets or sets whether the user must change password upon next login.
    /// </summary>
    public bool MustChangePassword { get; set; } = false;
}

