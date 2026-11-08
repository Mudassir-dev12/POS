using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.DependencyInjection;

using ShopPOS.Core;

using ShopPOS.Core.DTOs;

using ShopPOS.Core.DTOs.Backup;

using ShopPOS.Core.Entities;

using ShopPOS.Core.Enums;

using ShopPOS.Core.Interfaces;

using ShopPOS.Core.Models;

using ShopPOS.Core.Services;

using ShopPOS.Data;

using ShopPOS.UI.Services;

using ShopPOS.UI.ViewModels.BackupRestore;

using ShopPOS.UI.ViewModels.Dialogs;

using ShopPOS.UI.ViewModels.Purchases;

using Xunit;



namespace ShopPOS.Tests;



public class BackupViewModelTests : IDisposable

{

    private readonly string _testRoot;

    private readonly IAppPaths _appPaths;

    private readonly ServiceProvider _serviceProvider;

    private readonly AppDbContext _context;



    public BackupViewModelTests()

    {

        _testRoot = Path.Combine(Path.GetTempPath(), $"VmTest_{Guid.NewGuid():N}");

        _appPaths = new AppPaths(_testRoot);

        _appPaths.EnsureDirectoriesExist();



        var services = new ServiceCollection();

        services.AddSingleton<IAppPaths>(_appPaths);

        services.AddCoreServices();

        services.AddDataServices(new DatabaseOptions { DbPath = _appPaths.DatabasePath });

        services.AddScoped<IDialogService, TestDialogService>();



        _serviceProvider = services.BuildServiceProvider();

        _context = _serviceProvider.GetRequiredService<AppDbContext>();

        _context.Database.Migrate();



        _context.ShopSettings.Add(new ShopSetting { ShopName = "VM Test Store" });

        _context.SaveChanges();

    }



    [Fact]

    public async Task BackupRestoreViewModel_LoadsDataAndCreatesBackup()

    {

        var backupSvc = _serviceProvider.GetRequiredService<IBackupService>();

        var restoreSvc = _serviceProvider.GetRequiredService<IRestoreService>();

        var dialogSvc = _serviceProvider.GetRequiredService<IDialogService>();



        var vm = new BackupRestoreViewModel(backupSvc, restoreSvc, dialogSvc, _appPaths);

        await vm.LoadDataAsync();



        vm.Summary.Should().NotBeNull();

        vm.Settings.Should().NotBeNull();



        // Trigger BackupNow

        await vm.BackupNowAsync();



        vm.Backups.Count.Should().Be(1);

        vm.HasActionSuccess.Should().BeTrue();

    }



    [Fact]

    public void RestoreConfirmDialogViewModel_RequiresUppercaseRestoreConfirmation()

    {

        var restoreSvc = _serviceProvider.GetRequiredService<IRestoreService>();

        var vm = new RestoreConfirmDialogViewModel(restoreSvc);



        vm.TypedConfirmation = "restore";

        vm.CanRestore.Should().BeFalse("Must require exact uppercase RESTORE.");



        vm.TypedConfirmation = "RESTORE";

        vm.CanRestore.Should().BeTrue();



        vm.Confirm();

        vm.DialogResult.Should().BeTrue();

    }



    [Fact]

    public void BackupPasswordDialogViewModel_ValidatesPasswordRules()

    {

        var vm = new BackupPasswordDialogViewModel();



        // Too short

        vm.NewPassword = "123";

        vm.ConfirmPassword = "123";

        vm.Save();

        vm.DialogResult.Should().BeNull();

        vm.ValidationError.Should().Contain("at least 6 characters");



        // Passwords do not match

        vm.NewPassword = "Password123";

        vm.ConfirmPassword = "DifferentPassword456";

        vm.Save();

        vm.DialogResult.Should().BeNull();

        vm.ValidationError.Should().Contain("match");



        // Valid

        vm.NewPassword = "ValidPassword123!";

        vm.ConfirmPassword = "ValidPassword123!";

        vm.Save();

        vm.DialogResult.Should().BeTrue();

        vm.ValidationError.Should().BeNull();

    }



    [Fact]

    public void AppCloseBackupDialogViewModel_SkipSetsDialogResultFalse()

    {

        var vm = new AppCloseBackupDialogViewModel();

        vm.Skip();

        vm.IsSkipped.Should().BeTrue();

        vm.DialogResult.Should().BeFalse();

    }



    [Fact]
    public async Task BackupRestoreViewModel_SaveManualGoogleCredentials_CreatesConfigFileAndReloads()
    {
        var backupSvc = _serviceProvider.GetRequiredService<IBackupService>();
        var restoreSvc = _serviceProvider.GetRequiredService<IRestoreService>();
        var dialogSvc = _serviceProvider.GetRequiredService<IDialogService>();

        var vm = new BackupRestoreViewModel(backupSvc, restoreSvc, dialogSvc, _appPaths);
        await vm.LoadDataAsync();

        vm.GoogleClientIdInput = "test-client-id.apps.googleusercontent.com";
        vm.GoogleClientSecretInput = "GOCSPX-test-secret";

        await vm.SaveManualGoogleCredentialsAsync();

        string targetPath = Path.Combine(_appPaths.DataDirectory, "googledrive.client.json");
        File.Exists(targetPath).Should().BeTrue();

        string json = await File.ReadAllTextAsync(targetPath);
        json.Should().Contain("test-client-id.apps.googleusercontent.com");
        json.Should().Contain("GOCSPX-test-secret");

        // Clear credentials
        await vm.ClearGoogleCredentialsAsync();
        File.Exists(targetPath).Should().BeFalse();
    }

    public void Dispose()

    {

        _context.Dispose();

        _serviceProvider.Dispose();

        try { Directory.Delete(_testRoot, recursive: true); } catch { }

    }



    private class TestDialogService : IDialogService

    {

        public void ShowMessage(string message, string title = "Information") { }

        public bool ShowConfirmation(string message, string title = "Confirm") => true;

        public string? ShowSaveFileDialog(string filter, string defaultFileName, string title = "Save File") => null;

        public string? ShowOpenFileDialog(string filter, string title = "Open File") => null;

        public ConfirmActionResult ShowConfirmActionDialog(string title, string message, bool isReferenced) => ConfirmActionResult.Delete;

        public bool ShowBulkConfirmDialog(string actionName, int count, bool requireTypedConfirmation, string? detail = null) => true;

        public bool ShowBulkSummaryDialog(string title, BulkOperationResult result) => true;

        public bool? ShowCustomDialog(System.Windows.Window window) => true;

        public bool? ShowProductDialog(ShopPOS.UI.ViewModels.Products.ProductDialogViewModel viewModel) => true;

        public void ShowPriceHistoryDialog(ShopPOS.UI.ViewModels.Products.PriceHistoryDialogViewModel viewModel) { }

        public bool? ShowCsvImportWizardDialog(ShopPOS.UI.ViewModels.Products.CsvImportWizardViewModel viewModel) => true;

        public bool? ShowCategoryDialog(ShopPOS.UI.ViewModels.Categories.CategoryDialogViewModel viewModel) => true;

        public bool? ShowBrandDialog(ShopPOS.UI.ViewModels.Brands.BrandDialogViewModel viewModel) => true;

        public bool? ShowUnitDialog(ShopPOS.UI.ViewModels.Units.UnitDialogViewModel viewModel) => true;

        public bool? ShowSupplierDialog(ShopPOS.UI.ViewModels.Suppliers.SupplierDialogViewModel viewModel) => true;

        public bool? ShowPurchaseEntryDialog(ShopPOS.UI.ViewModels.Purchases.PurchaseEntryViewModel viewModel) => true;

        public bool? ShowSupplierPaymentDialog(ShopPOS.UI.ViewModels.SupplierPayments.SupplierPaymentDialogViewModel viewModel) => true;

        public bool? ShowPurchaseReturnDialog(ShopPOS.UI.ViewModels.PurchaseReturns.PurchaseReturnDialogViewModel viewModel) => true;

        public ShopPOS.Core.DTOs.ProductListItemDto? ShowProductSearchDialog(ShopPOS.UI.ViewModels.Purchases.ProductSearchDialogViewModel viewModel) => null;

        public IReadOnlyList<ShopPOS.Core.DTOs.ProductListItemDto> ShowMultiProductSearchDialog(ShopPOS.UI.ViewModels.Purchases.ProductSearchDialogViewModel viewModel) => Array.Empty<ShopPOS.Core.DTOs.ProductListItemDto>();

        public string? ShowReasonPromptDialog(string title, string message, string defaultReason = "") => defaultReason;

        public bool? ShowStockAdjustmentDialog(ShopPOS.UI.ViewModels.Inventory.StockAdjustmentDialogViewModel viewModel) => true;

        public bool? ShowSalePaymentDialog(ShopPOS.UI.ViewModels.Sales.SalePaymentDialogViewModel viewModel) => true;

        public bool? ShowSaleDetailDialog(ShopPOS.UI.ViewModels.Sales.SaleDetailDialogViewModel viewModel) => true;

        public bool? ShowSaleReturnDialog(ShopPOS.UI.ViewModels.Sales.SaleReturnDialogViewModel viewModel) => true;

        public bool? ShowHeldBillsDialog(ShopPOS.UI.ViewModels.HeldBills.HeldBillsDialogViewModel viewModel) => true;

        public bool? ShowBillDiscountDialog(ShopPOS.UI.ViewModels.Sales.BillDiscountDialogViewModel viewModel) => true;

        public bool? ShowLineDiscountDialog(ShopPOS.UI.ViewModels.Sales.LineDiscountDialogViewModel viewModel) => true;

        public void ShowPosHelpDialog(ShopPOS.UI.ViewModels.Sales.PosHelpDialogViewModel viewModel) { }

        public CartExitAction ShowCartExitPrompt() => CartExitAction.Keep;

        public void ShowReceiptPreviewDialog(ShopPOS.UI.ViewModels.Sales.ReceiptPreviewDialogViewModel viewModel) { }

        public void ShowReceiptPreviewDialog(ShopPOS.UI.ViewModels.Sales.SalePaymentDialogViewModel? unused, ShopPOS.UI.ViewModels.Sales.ReceiptPreviewDialogViewModel viewModel) { }

        public void ShowReportPreviewDialog(ShopPOS.UI.ViewModels.Reports.ReportPreviewDialogViewModel viewModel) { }

        public bool? ShowPrinterSettingsDialog(ShopPOS.UI.ViewModels.Settings.PrinterSettingsDialogViewModel viewModel) => true;

        public bool ShowDataResetConfirmDialog(DataResetConfirmDialogViewModel viewModel) => true;

        public void ShowDataToolResultDialog(DataToolResultDialogViewModel viewModel) { }

        public bool? ShowRestoreConfirmDialog(RestoreConfirmDialogViewModel viewModel) => true;

        public bool? ShowBackupPasswordDialog(BackupPasswordDialogViewModel viewModel) => true;

        public bool? ShowAppCloseBackupDialog(AppCloseBackupDialogViewModel viewModel) => true;

        public string? ShowFolderBrowserDialog(string? description = null, string? initialPath = null) => null;

        public bool? ShowDriveConnectProgressDialog(DriveConnectProgressDialogViewModel viewModel) => true;

        public bool? ShowDriveBackupsDialog(DriveBackupsDialogViewModel viewModel) => true;

        public bool? ShowGitHubRestoreDialog(ShopPOS.UI.ViewModels.Dialogs.GitHubRestoreDialogViewModel viewModel) => true;

        public bool? ShowGitHubCopyRepoDialog(ShopPOS.UI.ViewModels.Dialogs.GitHubCopyRepoDialogViewModel viewModel) => true;

        public bool? ShowSetupWizardDialog(ShopPOS.UI.ViewModels.Setup.SetupWizardViewModel viewModel) => true;

        public void ShowUnhandledExceptionDialog(string message, string? stackTrace, string? logDirectory = null) { }

    }

}



