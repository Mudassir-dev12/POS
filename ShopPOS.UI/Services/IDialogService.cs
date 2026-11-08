using ShopPOS.Core.DTOs;
using ShopPOS.UI.ViewModels.Brands;
using ShopPOS.UI.ViewModels.Categories;
using ShopPOS.UI.ViewModels.HeldBills;
using ShopPOS.UI.ViewModels.Inventory;
using ShopPOS.UI.ViewModels.Products;
using ShopPOS.UI.ViewModels.PurchaseReturns;
using ShopPOS.UI.ViewModels.Purchases;
using ShopPOS.UI.ViewModels.Sales;
using ShopPOS.UI.ViewModels.Settings;
using ShopPOS.UI.ViewModels.SupplierPayments;
using ShopPOS.UI.ViewModels.Suppliers;
using ShopPOS.UI.ViewModels.Units;

namespace ShopPOS.UI.Services;

public enum ConfirmActionResult
{
    Cancel,
    Delete,
    Deactivate
}

public interface IDialogService
{
    void ShowMessage(string message, string title = "Information");
    bool ShowConfirmation(string message, string title = "Confirm");
    string? ShowSaveFileDialog(string filter, string defaultFileName, string title = "Save File");
    string? ShowOpenFileDialog(string filter, string title = "Open File");
    ConfirmActionResult ShowConfirmActionDialog(string title, string message, bool isReferenced);
    bool ShowBulkConfirmDialog(string actionName, int count, bool requireTypedConfirmation, string? detail = null);
    bool ShowBulkSummaryDialog(string title, BulkOperationResult result);
    bool? ShowCustomDialog(System.Windows.Window window);

    bool? ShowProductDialog(ProductDialogViewModel viewModel);
    void ShowPriceHistoryDialog(PriceHistoryDialogViewModel viewModel);
    bool? ShowCsvImportWizardDialog(CsvImportWizardViewModel viewModel);

    bool? ShowCategoryDialog(CategoryDialogViewModel viewModel);
    bool? ShowBrandDialog(BrandDialogViewModel viewModel);
    bool? ShowUnitDialog(UnitDialogViewModel viewModel);
    bool? ShowSupplierDialog(SupplierDialogViewModel viewModel);

    bool? ShowPurchaseEntryDialog(PurchaseEntryViewModel viewModel);
    bool? ShowSupplierPaymentDialog(SupplierPaymentDialogViewModel viewModel);
    bool? ShowPurchaseReturnDialog(PurchaseReturnDialogViewModel viewModel);
    ProductListItemDto? ShowProductSearchDialog(ProductSearchDialogViewModel viewModel);
    IReadOnlyList<ProductListItemDto> ShowMultiProductSearchDialog(ProductSearchDialogViewModel viewModel);
    string? ShowReasonPromptDialog(string title, string message, string defaultReason = "");

    bool? ShowStockAdjustmentDialog(StockAdjustmentDialogViewModel viewModel);

    // Phase 5B Sales & POS Dialogs
    bool? ShowSalePaymentDialog(SalePaymentDialogViewModel viewModel);
    bool? ShowSaleDetailDialog(SaleDetailDialogViewModel viewModel);
    bool? ShowSaleReturnDialog(SaleReturnDialogViewModel viewModel);
    bool? ShowHeldBillsDialog(HeldBillsDialogViewModel viewModel);
    bool? ShowBillDiscountDialog(BillDiscountDialogViewModel viewModel);
    bool? ShowLineDiscountDialog(LineDiscountDialogViewModel viewModel);
    void ShowPosHelpDialog(PosHelpDialogViewModel viewModel);
    CartExitAction ShowCartExitPrompt();

    // Phase 5C Printing Dialogs
    void ShowReceiptPreviewDialog(ReceiptPreviewDialogViewModel viewModel);
    void ShowReceiptPreviewDialog(SalePaymentDialogViewModel? unused, ReceiptPreviewDialogViewModel viewModel);
    void ShowReportPreviewDialog(ViewModels.Reports.ReportPreviewDialogViewModel viewModel);
    bool? ShowPrinterSettingsDialog(PrinterSettingsDialogViewModel viewModel);

    // Phase 6A Data Tools Dialogs
    bool ShowDataResetConfirmDialog(ViewModels.Dialogs.DataResetConfirmDialogViewModel viewModel);
    void ShowDataToolResultDialog(ViewModels.Dialogs.DataToolResultDialogViewModel viewModel);

    // Phase 7A Backup & Restore Dialogs
    bool? ShowRestoreConfirmDialog(ViewModels.Dialogs.RestoreConfirmDialogViewModel viewModel);
    bool? ShowBackupPasswordDialog(ViewModels.Dialogs.BackupPasswordDialogViewModel viewModel);
    bool? ShowAppCloseBackupDialog(ViewModels.Dialogs.AppCloseBackupDialogViewModel viewModel);
    string? ShowFolderBrowserDialog(string? description = null, string? initialPath = null);

    // Phase 7B Google Drive Dialogs
    bool? ShowDriveConnectProgressDialog(ViewModels.Dialogs.DriveConnectProgressDialogViewModel viewModel);
    bool? ShowDriveBackupsDialog(ViewModels.Dialogs.DriveBackupsDialogViewModel viewModel);

    // Phase 7B GitHub Dialogs
    bool? ShowGitHubRestoreDialog(ViewModels.Dialogs.GitHubRestoreDialogViewModel viewModel);
    bool? ShowGitHubCopyRepoDialog(ViewModels.Dialogs.GitHubCopyRepoDialogViewModel viewModel);

    // Phase 7C Setup Wizard & Hardening
    bool? ShowSetupWizardDialog(ViewModels.Setup.SetupWizardViewModel viewModel);
    void ShowUnhandledExceptionDialog(string message, string? stackTrace, string? logDirectory = null);
}
