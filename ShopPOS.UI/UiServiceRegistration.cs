using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
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
using ShopPOS.UI.Views;
using ShopPOS.UI.Views.Dialogs;

namespace ShopPOS.UI;

public static class UiServiceRegistration
{
    public static IServiceCollection AddUiServices(this IServiceCollection services)
    {
        // UI Navigation & Dialogs
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PlaceholderViewModel>(sp => new PlaceholderViewModel("Module"));

        services.AddTransient<ProductsViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<BrandsViewModel>();
        services.AddTransient<UnitsViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<CustomersViewModel>();

        services.AddTransient<PurchasesViewModel>();
        services.AddTransient<PurchaseEntryViewModel>();
        services.AddTransient<ProductSearchDialogViewModel>();
        services.AddTransient<ReasonPromptDialogViewModel>(sp => new ReasonPromptDialogViewModel("Confirmation", "Are you sure?"));

        services.AddTransient<SupplierPaymentsViewModel>();
        services.AddTransient<SupplierPaymentDialogViewModel>();

        services.AddTransient<PurchaseReturnsViewModel>();
        services.AddTransient<PurchaseReturnDialogViewModel>();

        services.AddTransient<SupplierLedgerViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Phase 4 Inventory ViewModels
        services.AddTransient<StockOverviewViewModel>();
        services.AddTransient<StockMovementHistoryViewModel>();
        services.AddTransient<StockAdjustmentsViewModel>();
        services.AddTransient<StockAdjustmentDialogViewModel>(sp => new StockAdjustmentDialogViewModel(null, sp.GetRequiredService<IStockAdjustmentService>(), sp.GetRequiredService<IProductService>(), sp.GetRequiredService<IStockLedgerQuery>(), sp.GetRequiredService<IAuthService>(), sp.GetRequiredService<IDialogService>()));
        services.AddTransient<ReorderListViewModel>();
        services.AddTransient<StockValuationViewModel>();

        services.AddTransient<PriceHistoryDialogViewModel>();

        services.AddTransient<BrandDialogViewModel>(sp =>
        {
            var auth = sp.GetRequiredService<IAuthService>();
            return new BrandDialogViewModel(
                sp.GetRequiredService<IBrandService>(),
                auth.CurrentUser?.Id,
                auth.CurrentUser?.Role?.Name);
        });

        services.AddTransient<CategoryDialogViewModel>(sp =>
        {
            var auth = sp.GetRequiredService<IAuthService>();
            return new CategoryDialogViewModel(
                sp.GetRequiredService<ICategoryService>(),
                auth.CurrentUser?.Id,
                auth.CurrentUser?.Role?.Name);
        });

        services.AddTransient<UnitDialogViewModel>(sp =>
        {
            var auth = sp.GetRequiredService<IAuthService>();
            return new UnitDialogViewModel(
                sp.GetRequiredService<IUnitService>(),
                auth.CurrentUser?.Id,
                auth.CurrentUser?.Role?.Name);
        });

        services.AddTransient<SupplierDialogViewModel>(sp =>
        {
            var auth = sp.GetRequiredService<IAuthService>();
            return new SupplierDialogViewModel(
                sp.GetRequiredService<ISupplierService>(),
                auth.CurrentUser?.Id,
                auth.CurrentUser?.Role?.Name);
        });

        services.AddTransient<CustomerDialogViewModel>(sp =>
        {
            var auth = sp.GetRequiredService<IAuthService>();
            return new CustomerDialogViewModel(
                sp.GetRequiredService<ICustomerService>(),
                sp.GetRequiredService<IPriceLevelService>(),
                sp.GetRequiredService<IPermissionService>(),
                auth.CurrentUser?.Id,
                auth.CurrentUser?.Role?.Name);
        });

        services.AddTransient<ProductDialogViewModel>(sp =>
        {
            var auth = sp.GetRequiredService<IAuthService>();
            return new ProductDialogViewModel(
                sp.GetRequiredService<IProductService>(),
                sp.GetRequiredService<ICategoryService>(),
                sp.GetRequiredService<IBrandService>(),
                sp.GetRequiredService<IUnitService>(),
                sp.GetRequiredService<IPriceLevelService>(),
                sp.GetRequiredService<IDialogService>(),
                auth.CurrentUser?.Id,
                auth.CurrentUser?.Role?.Name);
        });

        services.AddTransient<CsvImportWizardViewModel>(sp =>
        {
            var auth = sp.GetRequiredService<IAuthService>();
            return new CsvImportWizardViewModel(
                sp.GetRequiredService<ICsvProductService>(),
                sp.GetRequiredService<IDialogService>(),
                auth.CurrentUser?.Id,
                auth.CurrentUser?.Role?.Name);
        });

        // Windows and Dialogs
        services.AddTransient<MainWindow>();
        services.AddTransient<PurchaseEntryWindow>();
        services.AddTransient<SupplierPaymentDialog>();
        services.AddTransient<PurchaseReturnDialog>();
        services.AddTransient<ProductSearchDialog>();
        services.AddTransient<ReasonPromptDialog>();
        services.AddTransient<ProductDialog>();
        services.AddTransient<CategoryDialog>();
        services.AddTransient<BrandDialog>();
        services.AddTransient<UnitDialog>();
        services.AddTransient<SupplierDialog>();
        services.AddTransient<CustomerDialog>();
        services.AddTransient<PriceHistoryDialog>();
        services.AddTransient<CsvImportWizardDialog>();
        services.AddTransient<ConfirmActionDialog>(sp => new ConfirmActionDialog("Confirm", "Are you sure?", false));
        services.AddTransient<StockAdjustmentDialog>(sp => new StockAdjustmentDialog(sp.GetRequiredService<StockAdjustmentDialogViewModel>()));

        // Views (UserControls)
        services.AddTransient<DashboardView>();
        services.AddTransient<ProductsView>();
        services.AddTransient<CategoriesView>();
        services.AddTransient<BrandsView>();
        services.AddTransient<UnitsView>();
        services.AddTransient<SuppliersView>();
        services.AddTransient<CustomersView>();
        services.AddTransient<PurchasesView>();
        services.AddTransient<SupplierPaymentsView>();
        services.AddTransient<PurchaseReturnsView>();
        services.AddTransient<SupplierLedgerView>();
        services.AddTransient<SettingsView>();
        services.AddTransient<StockOverviewView>();
        services.AddTransient<StockMovementHistoryView>();
        services.AddTransient<StockAdjustmentsView>();
        services.AddTransient<ReorderListView>();
        services.AddTransient<StockValuationView>();
        services.AddTransient<PlaceholderView>();

        return services;
    }
}
