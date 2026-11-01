namespace ShopPOS.UI.Services;

public enum ConfirmActionResult
{
    Cancel,
    Delete,
    Deactivate
}

public interface IDialogService
{
    bool? ShowProductDialog(ShopPOS.UI.ViewModels.Products.ProductDialogViewModel viewModel);
    void ShowPriceHistoryDialog(ShopPOS.UI.ViewModels.Products.PriceHistoryDialogViewModel viewModel);
    bool? ShowCsvImportWizardDialog(ShopPOS.UI.ViewModels.Products.CsvImportWizardViewModel viewModel);

    bool? ShowCategoryDialog(ShopPOS.UI.ViewModels.Categories.CategoryDialogViewModel viewModel);
    bool? ShowBrandDialog(ShopPOS.UI.ViewModels.Brands.BrandDialogViewModel viewModel);
    bool? ShowUnitDialog(ShopPOS.UI.ViewModels.Units.UnitDialogViewModel viewModel);
    bool? ShowSupplierDialog(ShopPOS.UI.ViewModels.Suppliers.SupplierDialogViewModel viewModel);
    bool? ShowCustomerDialog(ShopPOS.UI.ViewModels.Customers.CustomerDialogViewModel viewModel);

    bool? ShowPurchaseEntryDialog(ShopPOS.UI.ViewModels.Purchases.PurchaseEntryViewModel viewModel);
    bool? ShowSupplierPaymentDialog(ShopPOS.UI.ViewModels.SupplierPayments.SupplierPaymentDialogViewModel viewModel);
    bool? ShowPurchaseReturnDialog(ShopPOS.UI.ViewModels.PurchaseReturns.PurchaseReturnDialogViewModel viewModel);
    ShopPOS.Core.DTOs.ProductListItemDto? ShowProductSearchDialog(ShopPOS.UI.ViewModels.Purchases.ProductSearchDialogViewModel viewModel);
    System.Collections.Generic.IReadOnlyList<ShopPOS.Core.DTOs.ProductListItemDto> ShowMultiProductSearchDialog(ShopPOS.UI.ViewModels.Purchases.ProductSearchDialogViewModel viewModel);
    string? ShowReasonPromptDialog(string title, string message, string defaultReason = "");

    bool? ShowStockAdjustmentDialog(ShopPOS.UI.ViewModels.Inventory.StockAdjustmentDialogViewModel viewModel);

    void ShowMessage(string message, string title = "Information");
    bool ShowConfirmation(string message, string title = "Confirm");

    ConfirmActionResult ShowConfirmActionDialog(string title, string message, bool isReferenced);

    string? ShowOpenFileDialog(string filter, string title = "Open File");
    string? ShowSaveFileDialog(string filter, string defaultFileName, string title = "Save File");

    bool? ShowCustomDialog(System.Windows.Window window);
}

