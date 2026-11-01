using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopPOS.Core;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Data;
using ShopPOS.Data.Seeding;
using ShopPOS.UI;
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
using Xunit;

namespace ShopPOS.Tests;

public class UiSmokeTests
{
    private static void RunOnSta(Action action)
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            ExceptionDispatchInfo.Capture(threadEx).Throw();
        }
    }

    [Fact]
    public void AllViewsAndDialogs_MustLoadXamlAndBindSuccessfully_WithoutExceptionsOrMissingResources()
    {
        RunOnSta(() =>
        {
            // 1. Ensure WPF Application and App.xaml resources (converters, themes, styles) are loaded
            if (Application.Current == null)
            {
                var app = new App();
                app.InitializeComponent();
            }

            // 2. Build real DI container using temporary SQLite database
            string tempDbPath = Path.Combine(Path.GetTempPath(), $"ShopPOS_UiSmoke_{Guid.NewGuid():N}.db");
            var services = new ServiceCollection();
            var dbOptions = new DatabaseOptions { DbPath = tempDbPath };

            services.AddCoreServices();
            services.AddDataServices(dbOptions);
            services.AddUiServices();

            var serviceProvider = services.BuildServiceProvider();

            try
            {
                // 3. Migrate and seed the temporary database
                using (var scope = serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Database.Migrate();

                    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
                    seeder.SeedAsync().GetAwaiter().GetResult();
                }

                // 4. Test all Windows & Dialogs
                var windowTests = new (Type ViewType, Type ViewModelType)[]
                {
                    (typeof(MainWindow), typeof(MainWindowViewModel)),
                    (typeof(PurchaseEntryWindow), typeof(PurchaseEntryViewModel)),
                    (typeof(SupplierPaymentDialog), typeof(SupplierPaymentDialogViewModel)),
                    (typeof(PurchaseReturnDialog), typeof(PurchaseReturnDialogViewModel)),
                    (typeof(ProductSearchDialog), typeof(ProductSearchDialogViewModel)),
                    (typeof(ReasonPromptDialog), typeof(ReasonPromptDialogViewModel)),
                    (typeof(ProductDialog), typeof(ProductDialogViewModel)),
                    (typeof(CategoryDialog), typeof(CategoryDialogViewModel)),
                    (typeof(BrandDialog), typeof(BrandDialogViewModel)),
                    (typeof(UnitDialog), typeof(UnitDialogViewModel)),
                    (typeof(SupplierDialog), typeof(SupplierDialogViewModel)),
                    (typeof(CustomerDialog), typeof(CustomerDialogViewModel)),
                    (typeof(PriceHistoryDialog), typeof(PriceHistoryDialogViewModel)),
                    (typeof(CsvImportWizardDialog), typeof(CsvImportWizardViewModel)),
                    (typeof(StockAdjustmentDialog), typeof(StockAdjustmentDialogViewModel)),
                };

                foreach (var (viewType, vmType) in windowTests)
                {
                    Console.WriteLine($"[SMOKE TEST] Testing window {viewType.Name}...");
                    var vm = serviceProvider.GetRequiredService(vmType);
                    var window = (Window)serviceProvider.GetRequiredService(viewType);
                    window.DataContext = vm;

                    // Force XAML load, layout, and rendering
                    window.Show();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    window.Close();
                }

                // ConfirmActionDialog (non-VM custom dialog)
                Console.WriteLine("[SMOKE TEST] Testing ConfirmActionDialog...");
                var confirmDialog = serviceProvider.GetRequiredService<ConfirmActionDialog>();
                confirmDialog.Show();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                confirmDialog.Close();

                // 5. Test all UserControls (Views) hosted inside a temporary test window
                var userControlTests = new (Type ViewType, Type ViewModelType)[]
                {
                    (typeof(ProductsView), typeof(ProductsViewModel)),
                    (typeof(CategoriesView), typeof(CategoriesViewModel)),
                    (typeof(BrandsView), typeof(BrandsViewModel)),
                    (typeof(UnitsView), typeof(UnitsViewModel)),
                    (typeof(SuppliersView), typeof(SuppliersViewModel)),
                    (typeof(CustomersView), typeof(CustomersViewModel)),
                    (typeof(PurchasesView), typeof(PurchasesViewModel)),
                    (typeof(SupplierPaymentsView), typeof(SupplierPaymentsViewModel)),
                    (typeof(PurchaseReturnsView), typeof(PurchaseReturnsViewModel)),
                    (typeof(SupplierLedgerView), typeof(SupplierLedgerViewModel)),
                    (typeof(SettingsView), typeof(SettingsViewModel)),
                    (typeof(PlaceholderView), typeof(PlaceholderViewModel)),
                    (typeof(StockOverviewView), typeof(StockOverviewViewModel)),
                    (typeof(StockMovementHistoryView), typeof(StockMovementHistoryViewModel)),
                    (typeof(StockAdjustmentsView), typeof(StockAdjustmentsViewModel)),
                    (typeof(ReorderListView), typeof(ReorderListViewModel)),
                    (typeof(StockValuationView), typeof(StockValuationViewModel)),
                    (typeof(DashboardView), typeof(DashboardViewModel))
                };

                foreach (var (viewType, vmType) in userControlTests)
                {
                    Console.WriteLine($"[SMOKE TEST] Testing UserControl {viewType.Name}...");
                    var vm = serviceProvider.GetRequiredService(vmType);
                    var view = (FrameworkElement)serviceProvider.GetRequiredService(viewType);
                    view.DataContext = vm;

                    var hostWindow = new Window
                    {
                        Content = view,
                        Width = 900,
                        Height = 600
                    };

                    hostWindow.Show();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    hostWindow.Close();
                }
            }
            finally
            {
                if (File.Exists(tempDbPath))
                {
                    try { File.Delete(tempDbPath); } catch { /* best effort cleanup */ }
                }
            }
        });
    }
}
