using ShopPOS.Core.DTOs;
using ShopPOS.UI.ViewModels.Brands;
using ShopPOS.UI.ViewModels.Categories;
using ShopPOS.UI.ViewModels.Customers;
using ShopPOS.UI.ViewModels.HeldBills;
using ShopPOS.UI.ViewModels.Inventory;
using ShopPOS.UI.ViewModels.Products;
using ShopPOS.UI.ViewModels.PurchaseReturns;
using ShopPOS.UI.ViewModels.Purchases;
using ShopPOS.UI.ViewModels.Sales;
using ShopPOS.UI.ViewModels.Shifts;
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
    bool? ShowProductDialog(ProductDialogViewModel viewModel);
    void ShowPriceHistoryDialog(PriceHistoryDialogViewModel viewModel);
    bool? ShowCsvImportWizardDialog(CsvImportWizardViewModel viewModel);

    bool? ShowCategoryDialog(CategoryDialogViewModel viewModel);
    bool? ShowBrandDialog(BrandDialogViewModel viewModel);
    bool? ShowUnitDialog(UnitDialogViewModel viewModel);
    bool? ShowSupplierDialog(SupplierDialogViewModel viewModel);
    bool? ShowCustomerDialog(CustomerDialogViewModel viewModel);

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
    bool? ShowOpenShiftDialog(OpenShiftDialogViewModel viewModel);
    bool? ShowCloseShiftDialog(CloseShiftDialogViewModel viewModel);
    bool? ShowCashMovementDialog(CashMovementDialogViewModel viewModel);
    bool? ShowHeldBillsDialog(HeldBillsDialogViewModel viewModel);
    bool? ShowBillDiscountDialog(BillDiscountDialogViewModel viewModel);
    bool? ShowLineDiscountDialog(LineDiscountDialogViewModel viewModel);
    void ShowPosHelpDialog(PosHelpDialogViewModel viewModel);
    CartExitAction ShowCartExitPrompt();

    // Phase 5C Printing Dialogs
    void ShowReceiptPreviewDialog(SalePaymentDialogViewModel? unused, ReceiptPreviewDialogViewModel viewModel);
    void ShowReceiptPreviewDialog(ReceiptPreviewDialogViewModel viewModel);
    bool? ShowPrinterSettingsDialog(ViewModels.Settings.PrinterSettingsDialogViewModel viewModel);

    void ShowMessage(string message, string title = "Information");
    bool ShowConfirmation(string message, string title = "Confirm");

    ConfirmActionResult ShowConfirmActionDialog(string title, string message, bool isReferenced);

    string? ShowOpenFileDialog(string filter, string title = "Open File");
    string? ShowSaveFileDialog(string filter, string defaultFileName, string title = "Save File");

    bool? ShowCustomDialog(System.Windows.Window window);
}
