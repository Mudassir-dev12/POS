using ShopPOS.Core.Interfaces;

namespace ShopPOS.Core.Services;

/// <summary>
/// Unrestricted permission service for the single-user application. All operations are permitted.
/// </summary>
public class PermissionService : IPermissionService
{
    public const string MenuDashboard = "Dashboard";
    public const string MenuProducts = "Products";
    public const string MenuCategories = "Categories";
    public const string MenuBrands = "Brands";
    public const string MenuUnits = "Units";
    public const string MenuPurchases = "Purchases";
    public const string MenuSupplierPayments = "SupplierPayments";
    public const string MenuPurchaseReturns = "PurchaseReturns";
    public const string MenuSupplierLedger = "SupplierLedger";
    public const string MenuSales = "Sales";
    public const string MenuPos = "POS";
    public const string MenuInventory = "Inventory";
    public const string MenuCustomers = "Customers";
    public const string MenuSuppliers = "Suppliers";
    public const string MenuReports = "Reports";
    public const string MenuSettings = "Settings";
    public const string MenuStockOverview = "StockOverview";
    public const string MenuStockMovements = "StockMovements";
    public const string MenuStockAdjustments = "StockAdjustments";
    public const string MenuReorder = "Reorder";
    public const string MenuValuation = "Valuation";

    private static readonly string[] AllMenus =
    [
        MenuDashboard,
        MenuProducts,
        MenuCategories,
        MenuBrands,
        MenuUnits,
        MenuPurchases,
        MenuSupplierPayments,
        MenuPurchaseReturns,
        MenuSupplierLedger,
        MenuStockOverview,
        MenuStockMovements,
        MenuStockAdjustments,
        MenuReorder,
        MenuValuation,
        MenuSales,
        MenuInventory,
        MenuCustomers,
        MenuSuppliers,
        MenuReports,
        MenuSettings
    ];

    public bool CanAccessMenuItem(string? roleName, string menuKey) => true;

    public IReadOnlyList<string> GetAccessibleMenuItems(string? roleName) => AllMenus;

    public bool CanManageUsers(string? roleName) => true;

    public bool CanManageCatalog(string? roleName) => true;

    public bool CanAdjustStock(string? roleName) => true;

    public bool CanAccessSuppliers(string? roleName) => true;

    public bool CanCreateEditCustomers(string? roleName) => true;

    public bool CanDeleteCustomers(string? roleName) => true;

    public bool CanManagePurchases(string? roleName) => true;

    public bool CanAccessInventory(string? roleName) => true;

    public bool CanManageInventory(string? roleName) => true;
}
