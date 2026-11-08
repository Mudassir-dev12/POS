using ShopPOS.Core.DTOs;
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
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels;
using ShopPOS.UI.ViewModels.Brands;
using ShopPOS.UI.ViewModels.Categories;
using ShopPOS.UI.ViewModels.HeldBills;
using ShopPOS.UI.ViewModels.Inventory;
using ShopPOS.UI.ViewModels.Products;
using ShopPOS.UI.ViewModels.PurchaseReturns;
using ShopPOS.UI.ViewModels.Purchases;
using ShopPOS.UI.ViewModels.Sales;
using ShopPOS.UI.ViewModels.Settings;
using ShopPOS.UI.ViewModels.SupplierLedger;
using ShopPOS.UI.ViewModels.SupplierPayments;
using ShopPOS.UI.ViewModels.Suppliers;
using ShopPOS.UI.ViewModels.Units;
using ShopPOS.UI.ViewModels.Dashboard;
using ShopPOS.UI.ViewModels.Reports;
using ShopPOS.UI.ViewModels.Dialogs;
using ShopPOS.UI.ViewModels.BackupRestore;
using ShopPOS.UI.Views;
using ShopPOS.UI.Views.Dialogs;
using ShopPOS.UI.Views.Dialogs.Sales;
using Xunit;

namespace ShopPOS.Tests;

public class UiSmokeTests
{
    private static void RunOnSta(Action action) => StaTestHelper.RunOnSta(action);

    [Fact]
    public void AllViewsAndDialogs_MustLoadXamlAndBindSuccessfully_InLightAndDarkThemes()
    {
        RunOnSta(() =>
        {
            // 1. WPF Application and App.xaml resources are initialized via StaTestHelper

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

                var themeService = serviceProvider.GetRequiredService<IThemeService>();

                var windowTests = new (Type ViewType, Type ViewModelType)[]
                {
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
                    (typeof(PriceHistoryDialog), typeof(PriceHistoryDialogViewModel)),
                    (typeof(CsvImportWizardDialog), typeof(CsvImportWizardViewModel)),
                    (typeof(StockAdjustmentDialog), typeof(StockAdjustmentDialogViewModel)),

                    // Phase 5B Windows and Dialogs
                    (typeof(SalePaymentDialog), typeof(SalePaymentDialogViewModel)),
                    (typeof(SaleDetailDialog), typeof(SaleDetailDialogViewModel)),
                    (typeof(SaleReturnDialog), typeof(SaleReturnDialogViewModel)),

                    (typeof(HeldBillsDialog), typeof(HeldBillsDialogViewModel)),
                    (typeof(BillDiscountDialog), typeof(BillDiscountDialogViewModel)),
                    (typeof(LineDiscountDialog), typeof(LineDiscountDialogViewModel)),
                    (typeof(PosHelpDialog), typeof(PosHelpDialogViewModel)),
                    (typeof(CartExitPromptDialog), typeof(CartExitPromptDialogViewModel)),
                    (typeof(PrinterSettingsDialog), typeof(PrinterSettingsDialogViewModel)),
                    (typeof(ReceiptPreviewDialog), typeof(ReceiptPreviewDialogViewModel)),
                    (typeof(ReportPreviewDialog), typeof(ReportPreviewDialogViewModel)),
                    (typeof(DataResetConfirmDialog), typeof(DataResetConfirmDialogViewModel)),
                    (typeof(DataToolResultDialog), typeof(DataToolResultDialogViewModel)),
                    (typeof(RestoreConfirmDialog), typeof(RestoreConfirmDialogViewModel)),
                    (typeof(BackupPasswordDialog), typeof(BackupPasswordDialogViewModel)),
                    (typeof(AppCloseBackupDialog), typeof(AppCloseBackupDialogViewModel)),
                    (typeof(DriveConnectProgressDialog), typeof(DriveConnectProgressDialogViewModel)),
                    (typeof(DriveBackupsDialog), typeof(DriveBackupsDialogViewModel)),
                    (typeof(ShopPOS.UI.Views.Setup.SetupWizardWindow), typeof(ShopPOS.UI.ViewModels.Setup.SetupWizardViewModel)),
                };

                var userControlTests = new (Type ViewType, Type ViewModelType)[]
                {
                    (typeof(ProductsView), typeof(ProductsViewModel)),
                    (typeof(CategoriesView), typeof(CategoriesViewModel)),
                    (typeof(BrandsView), typeof(BrandsViewModel)),
                    (typeof(UnitsView), typeof(UnitsViewModel)),
                    (typeof(SuppliersView), typeof(SuppliersViewModel)),
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
                    (typeof(DashboardView), typeof(DashboardViewModel)),

                    // Phase 5B Views
                    (typeof(PosSaleView), typeof(PosSaleViewModel)),
                    (typeof(SalesView), typeof(SalesViewModel)),
                    (typeof(SaleReturnsView), typeof(SaleReturnsViewModel)),
                    (typeof(ReportsView), typeof(ReportsViewModel)),
                    (typeof(BackupRestoreView), typeof(BackupRestoreViewModel)),
                };

                // Test under both Light and Dark themes
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    Console.WriteLine($"[SMOKE TEST] Testing UI under {theme} theme...");
                    themeService.SetTheme(theme);

                    // 4. Test all Windows & Dialogs
                    foreach (var (viewType, vmType) in windowTests)
                    {
                        Console.WriteLine($"[SMOKE TEST] Instantiating Window: {viewType.Name}, App={Application.Current != null}");
                        var vm = serviceProvider.GetRequiredService(vmType);
                        Console.WriteLine($"[SMOKE TEST] Resolved VM for {viewType.Name}, App={Application.Current != null}");
                        var window = (Window)serviceProvider.GetRequiredService(viewType);
                        Console.WriteLine($"[SMOKE TEST] Resolved Window for {viewType.Name}, App={Application.Current != null}");
                        window.DataContext = vm;
                        window.Show();
                        Console.WriteLine($"[SMOKE TEST] After Show for {viewType.Name}, App={Application.Current != null}");
                        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                        Console.WriteLine($"[SMOKE TEST] After Render for {viewType.Name}, App={Application.Current != null}");
                        window.Hide();
                        Console.WriteLine($"[SMOKE TEST] After Hide for {viewType.Name}, App={Application.Current != null}");
                    }

                    // ConfirmActionDialog (non-VM custom dialog)
                    var confirmDialog = serviceProvider.GetRequiredService<ConfirmActionDialog>();
                    confirmDialog.Show();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    confirmDialog.Close();

                    // BulkConfirmDialog
                    var bulkConfirm = new BulkConfirmDialog("Delete", 10, false);
                    bulkConfirm.Show();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    bulkConfirm.Close();

                    // BulkSummaryDialog
                    var bulkSummary = new BulkSummaryDialog("Summary", new BulkOperationResult());
                    bulkSummary.Show();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    bulkSummary.Close();

                    // UnhandledExceptionDialog
                    var unhandledExDialog = new UnhandledExceptionDialog("Test Exception Message", "at ShopPOS.Test.Mock() in C:\\test.cs:line 12");
                    unhandledExDialog.Show();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    unhandledExDialog.Close();

                    // MainWindow shell test
                    var mainVm = serviceProvider.GetRequiredService<MainWindowViewModel>();
                    var mainWin = (MainWindow)serviceProvider.GetRequiredService(typeof(MainWindow));
                    mainWin.DataContext = mainVm;
                    mainWin.Show();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    mainWin.Hide();

                    // 5. Test all UserControls (Views) hosted inside a temporary test window
                    foreach (var (viewType, vmType) in userControlTests)
                    {
                        Console.WriteLine($"[SMOKE TEST] Instantiating View: {viewType.Name}");
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
