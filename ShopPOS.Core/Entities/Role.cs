using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents a user authorization role (e.g., Admin, Manager, Cashier).
/// </summary>
public class Role : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique name of the role.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Navigation collection of users assigned to this role.
    /// </summary>
    public ICollection<User> Users { get; set; } = new List<User>();
}

