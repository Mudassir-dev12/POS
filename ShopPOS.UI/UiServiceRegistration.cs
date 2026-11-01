using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels;
using ShopPOS.UI.ViewModels.Brands;
using ShopPOS.UI.ViewModels.Categories;
using ShopPOS.UI.ViewModels.Customers;
using ShopPOS.UI.ViewModels.HeldBills;
using ShopPOS.UI.ViewModels.Inventory;
using ShopPOS.UI.ViewModels.Products;
using ShopPOS.UI.ViewModels.PurchaseReturns;
using ShopPOS.UI.ViewModels.Purchases;
using ShopPOS.UI.ViewModels.Sales;
using ShopPOS.UI.ViewModels.Settings;
using ShopPOS.UI.ViewModels.Shifts;
using ShopPOS.UI.ViewModels.SupplierLedger;
using ShopPOS.UI.ViewModels.SupplierPayments;
using ShopPOS.UI.ViewModels.Suppliers;
using ShopPOS.UI.ViewModels.Units;
using ShopPOS.UI.ViewModels.Dashboard;
using ShopPOS.UI.Views;
using ShopPOS.UI.Views.Dialogs;
using ShopPOS.UI.Views.Dialogs.Sales;

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

        // Phase 5B POS & Sales ViewModels
        services.AddTransient<PosSaleViewModel>();
        services.AddTransient<SalesViewModel>();
        services.AddTransient<SaleReturnsViewModel>();
        services.AddTransient<ShiftsViewModel>();
        services.AddTransient<HeldBillsDialogViewModel>();
        services.AddTransient<PosHelpDialogViewModel>();
        services.AddTransient<CartExitPromptDialogViewModel>();
        services.AddTransient<BillDiscountDialogViewModel>(sp => new BillDiscountDialogViewModel(0m, 0m));
        services.AddTransient<LineDiscountDialogViewModel>(sp => new LineDiscountDialogViewModel("Item", 10m, 1m, 0m, 0m));
        services.AddTransient<OpenShiftDialogViewModel>(sp => new OpenShiftDialogViewModel(sp.GetRequiredService<IShiftService>()));
        services.AddTransient<CloseShiftDialogViewModel>(sp => new CloseShiftDialogViewModel(1, "SHIFT-1", 100m, "$", sp.GetRequiredService<IShiftService>()));
        services.AddTransient<CashMovementDialogViewModel>(sp => new CashMovementDialogViewModel(1, "$", sp.GetRequiredService<IShiftService>()));
        services.AddTransient<SalePaymentDialogViewModel>(sp => new SalePaymentDialogViewModel(100m, null, "Walk-in", 0m, 0m, true, "$", sp.GetRequiredService<ISaleCalculator>()));
        services.AddTransient<SaleDetailDialogViewModel>(sp => new SaleDetailDialogViewModel(1, sp.GetRequiredService<ISaleService>(), sp.GetRequiredService<IPdfReportService>(), sp.GetRequiredService<IShopSettingService>()));
        services.AddTransient<SaleReturnDialogViewModel>(sp => new SaleReturnDialogViewModel(null, null, sp.GetRequiredService<ISaleReturnService>(), sp.GetRequiredService<ISaleService>(), sp.GetRequiredService<IShopSettingService>(), sp.GetRequiredService<IDialogService>()));
        services.AddTransient<PrinterSettingsDialogViewModel>(sp => new PrinterSettingsDialogViewModel(sp.GetRequiredService<IShopSettingService>(), sp.GetRequiredService<IReceiptPrinterService>()));
        services.AddTransient<ReceiptPreviewDialogViewModel>();

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

        // Phase 5B Windows and Dialogs
        services.AddTransient<SalePaymentDialog>();
        services.AddTransient<SaleDetailDialog>();
        services.AddTransient<SaleReturnDialog>();
        services.AddTransient<OpenShiftDialog>();
        services.AddTransient<CloseShiftDialog>();
        services.AddTransient<CashMovementDialog>();
        services.AddTransient<HeldBillsDialog>();
        services.AddTransient<BillDiscountDialog>();
        services.AddTransient<LineDiscountDialog>();
        services.AddTransient<PosHelpDialog>();
        services.AddTransient<CartExitPromptDialog>();
        services.AddTransient<PrinterSettingsDialog>();
        services.AddTransient<ReceiptPreviewDialog>();

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

        // Phase 5B Views
        services.AddTransient<PosSaleView>();
        services.AddTransient<SalesView>();
        services.AddTransient<SaleReturnsView>();
        services.AddTransient<ShiftsView>();

        return services;
    }
}
