using AwesomeAssertions;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Services;

namespace ShopPOS.Tests;

public class PermissionServiceTests
{
    private readonly PermissionService _sut = new();

    [Theory]
    [InlineData(RoleDefaults.Admin)]
    [InlineData(RoleDefaults.Manager)]
    [InlineData(RoleDefaults.Cashier)]
    [InlineData("Owner")]
    [InlineData(null)]
    public void SingleUserApplication_ShouldGrantAllPermissions(string? roleName)
    {
        // Act & Assert
        _sut.CanAccessMenuItem(roleName, "Dashboard").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Products").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Categories").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Brands").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Units").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Purchases").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "SupplierPayments").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "PurchaseReturns").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "SupplierLedger").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Sales").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "POS").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Inventory").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Customers").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Suppliers").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Reports").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Settings").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "StockOverview").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "StockMovements").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "StockAdjustments").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Reorder").Should().BeTrue();
        _sut.CanAccessMenuItem(roleName, "Valuation").Should().BeTrue();

        _sut.CanManageCatalog(roleName).Should().BeTrue();
        _sut.CanAdjustStock(roleName).Should().BeTrue();
        _sut.CanAccessSuppliers(roleName).Should().BeTrue();
        _sut.CanCreateEditCustomers(roleName).Should().BeTrue();
        _sut.CanDeleteCustomers(roleName).Should().BeTrue();
        _sut.CanManageUsers(roleName).Should().BeTrue();
        _sut.CanManagePurchases(roleName).Should().BeTrue();
        _sut.CanAccessInventory(roleName).Should().BeTrue();
        _sut.CanManageInventory(roleName).Should().BeTrue();

        _sut.GetAccessibleMenuItems(roleName).Should().NotBeEmpty();
    }
}
