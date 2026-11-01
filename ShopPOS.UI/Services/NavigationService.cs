using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.UI.ViewModels;
using ShopPOS.UI.ViewModels.Brands;
using ShopPOS.UI.ViewModels.Categories;
using ShopPOS.UI.ViewModels.Customers;
using ShopPOS.UI.ViewModels.Products;
using ShopPOS.UI.ViewModels.PurchaseReturns;
using ShopPOS.UI.ViewModels.Purchases;
using ShopPOS.UI.ViewModels.Settings;
using ShopPOS.UI.ViewModels.SupplierLedger;
using ShopPOS.UI.ViewModels.SupplierPayments;
using ShopPOS.UI.ViewModels.Suppliers;
using ShopPOS.UI.ViewModels.Units;
using ShopPOS.UI.ViewModels.Inventory;
using ShopPOS.UI.ViewModels.Dashboard;

namespace ShopPOS.UI.Services;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private ObservableObject? _currentView;

    public ObservableObject? CurrentView => _currentView;

    public event Action<ObservableObject>? CurrentViewChanged;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo(string menuKey) => NavigateTo(menuKey, null);

    public void NavigateTo(string menuKey, object? parameter)
    {
        var viewModel = CreateViewModelForMenuKey(menuKey, parameter);
        _currentView = viewModel;
        CurrentViewChanged?.Invoke(viewModel);
    }

    private ObservableObject CreateViewModelForMenuKey(string menuKey, object? parameter)
    {
        string normalizedKey = menuKey.Replace(" ", string.Empty).ToUpperInvariant();

        switch (normalizedKey)
        {
            case "PRODUCTS":
                return _serviceProvider.GetRequiredService<ProductsViewModel>();
            case "CATEGORIES":
                return _serviceProvider.GetRequiredService<CategoriesViewModel>();
            case "BRANDS":
                return _serviceProvider.GetRequiredService<BrandsViewModel>();
            case "UNITS":
                return _serviceProvider.GetRequiredService<UnitsViewModel>();
            case "CUSTOMERS":
                return _serviceProvider.GetRequiredService<CustomersViewModel>();
            case "SUPPLIERS":
                return _serviceProvider.GetRequiredService<SuppliersViewModel>();
            case "PURCHASES":
                return _serviceProvider.GetRequiredService<PurchasesViewModel>();
            case "SUPPLIERPAYMENTS":
                return _serviceProvider.GetRequiredService<SupplierPaymentsViewModel>();
            case "PURCHASERETURNS":
                return _serviceProvider.GetRequiredService<PurchaseReturnsViewModel>();
            case "SUPPLIERLEDGER":
            {
                var ledgerVm = _serviceProvider.GetRequiredService<SupplierLedgerViewModel>();
                if (parameter is int supplierId)
                {
                    _ = ledgerVm.InitializeAsync(supplierId);
                }
                return ledgerVm;
            }
            case "SETTINGS":
                return _serviceProvider.GetRequiredService<SettingsViewModel>();

            case "DASHBOARD":
                return _serviceProvider.GetRequiredService<DashboardViewModel>();

            case "STOCKOVERVIEW" or "INVENTORY":
            {
                var stockVm = _serviceProvider.GetRequiredService<StockOverviewViewModel>();
                if (parameter is ShopPOS.Core.Enums.StockStatus status)
                {
                    stockVm.SelectedStatus = status;
                }
                return stockVm;
            }

            case "STOCKMOVEMENTS":
                return _serviceProvider.GetRequiredService<StockMovementHistoryViewModel>();

            case "STOCKADJUSTMENTS":
                return _serviceProvider.GetRequiredService<StockAdjustmentsViewModel>();

            case "REORDER":
                return _serviceProvider.GetRequiredService<ReorderListViewModel>();

            case "VALUATION":
                return _serviceProvider.GetRequiredService<StockValuationViewModel>();

            case "SALES" or "POS":
                return new PlaceholderViewModel(
                    "Point of Sale (POS)",
                    "MoneyHand24",
                    "High-speed checkout counter, barcode scanner support, discounts, and thermal printing.");

            case "REPORTS":
                return new PlaceholderViewModel(
                    "Reports & Analytics",
                    "ChartMultiple24",
                    "Sales analytics, stock valuation, profit & loss reports, and tax declarations.");

            default:
                return new PlaceholderViewModel(menuKey, "Apps24", "Placeholder for module: " + menuKey);
        }
    }
}
