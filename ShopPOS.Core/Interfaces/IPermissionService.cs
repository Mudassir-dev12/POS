namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Provides role-based authorization and navigation access verification.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Checks if a specified role can access a menu item or feature key.
    /// </summary>
    bool CanAccessMenuItem(string? roleName, string menuKey);

    /// <summary>
    /// Returns the list of menu keys accessible to the given role.
    /// </summary>
    IReadOnlyList<string> GetAccessibleMenuItems(string? roleName);

    /// <summary>
    /// Determines whether the given role is authorized for user management operations.
    /// </summary>
    bool CanManageUsers(string? roleName);

    /// <summary>
    /// Determines whether the given role is authorized to create/edit/delete catalog items (Products, Categories, Brands, Units).
    /// </summary>
    bool CanManageCatalog(string? roleName);

    /// <summary>
    /// Determines whether the given role can adjust inventory stock quantities.
    /// Only Admin and Manager can adjust stock.
    /// </summary>
    bool CanAdjustStock(string? roleName);

    /// <summary>
    /// Determines whether the given role can access and manage suppliers.
    /// </summary>
    bool CanAccessSuppliers(string? roleName);

    /// <summary>
    /// Determines whether the given role can create or edit customers.
    /// </summary>
    bool CanCreateEditCustomers(string? roleName);

    /// <summary>
    /// Determines whether the given role can delete or deactivate customers.
    /// </summary>
    bool CanDeleteCustomers(string? roleName);

    /// <summary>
    /// Determines whether the given role can access and manage purchases, supplier payments, returns, and supplier ledger.
    /// </summary>
    bool CanManagePurchases(string? roleName);

    /// <summary>
    /// Determines whether the given role can access inventory screens.
    /// Admin, Manager, and Cashier (Stock Overview only) can access.
    /// </summary>
    bool CanAccessInventory(string? roleName);

    /// <summary>
    /// Determines whether the given role can manage inventory (adjustments, counts, valuation, reorder, history).
    /// Admin and Manager only.
    /// </summary>
    bool CanManageInventory(string? roleName);
}
