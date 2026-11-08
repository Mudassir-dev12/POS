using System.IO;

using System.Windows;

using Microsoft.Win32;

using ShopPOS.Core.DTOs;

using ShopPOS.UI.ViewModels.Brands;

using ShopPOS.UI.ViewModels.Categories;

using ShopPOS.UI.ViewModels.HeldBills;

using ShopPOS.UI.ViewModels.Inventory;

using ShopPOS.UI.ViewModels.Products;

using ShopPOS.UI.ViewModels.PurchaseReturns;

using ShopPOS.UI.ViewModels.Purchases;

using ShopPOS.UI.ViewModels.Sales;

using ShopPOS.UI.ViewModels.SupplierPayments;

using ShopPOS.UI.ViewModels.Suppliers;

using ShopPOS.UI.ViewModels.Units;

using ShopPOS.UI.Views.Dialogs;

using ShopPOS.UI.Views.Dialogs.Sales;



namespace ShopPOS.UI.Services;



public class DialogService : IDialogService

{

    public static Window? GetSafeOwnerWindow()

    {

        if (Application.Current == null)

        {

            return null;

        }



        // 1. Try currently active visible/loaded window

        var activeWindow = Application.Current.Windows

            .OfType<Window>()

            .FirstOrDefault(w => w.IsActive && (w.IsLoaded || w.IsVisible));



        if (activeWindow != null)

        {

            return activeWindow;

        }



        // 2. Otherwise try visible/loaded main window

        var mainWindow = Application.Current.MainWindow;

        if (mainWindow != null && (mainWindow.IsLoaded || mainWindow.IsVisible))

        {

            return mainWindow;

        }



        // 3. Otherwise try any loaded/visible window in application

        var anyVisibleWindow = Application.Current.Windows

            .OfType<Window>()

            .LastOrDefault(w => w.IsLoaded || w.IsVisible);



        return anyVisibleWindow;

    }



    private static void ConfigureDialogOwner(Window dialog)

    {

        var owner = GetSafeOwnerWindow();

        if (owner != null && dialog != owner)

        {

            try

            {

                dialog.Owner = owner;

                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                return;

            }

            catch (InvalidOperationException)

            {

                dialog.Owner = null;

            }

        }



        dialog.Owner = null;

        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

    }



    public bool? ShowProductDialog(ProductDialogViewModel viewModel)

    {

        var dialog = new ProductDialog(viewModel);

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public void ShowPriceHistoryDialog(PriceHistoryDialogViewModel viewModel)

    {

        var dialog = new PriceHistoryDialog(viewModel);

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();

    }



    public bool? ShowCsvImportWizardDialog(CsvImportWizardViewModel viewModel)

    {

        var dialog = new CsvImportWizardDialog(viewModel);

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowCategoryDialog(CategoryDialogViewModel viewModel)

    {

        var dialog = new CategoryDialog(viewModel);

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowBrandDialog(BrandDialogViewModel viewModel)

    {

        var dialog = new BrandDialog(viewModel);

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowUnitDialog(UnitDialogViewModel viewModel)

    {

        var dialog = new UnitDialog(viewModel);

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowSupplierDialog(SupplierDialogViewModel viewModel)

    {

        var dialog = new SupplierDialog(viewModel);

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowPurchaseEntryDialog(PurchaseEntryViewModel viewModel)

    {

        var dialog = new PurchaseEntryWindow(viewModel);

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowSupplierPaymentDialog(SupplierPaymentDialogViewModel viewModel)

    {

        var dialog = new SupplierPaymentDialog(viewModel);

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowPurchaseReturnDialog(PurchaseReturnDialogViewModel viewModel)

    {

        var dialog = new PurchaseReturnDialog(viewModel);

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public ProductListItemDto? ShowProductSearchDialog(ProductSearchDialogViewModel viewModel)

    {

        var dialog = new ProductSearchDialog(viewModel);

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();

        return dialog.SelectedProduct;

    }



    public IReadOnlyList<ProductListItemDto> ShowMultiProductSearchDialog(ProductSearchDialogViewModel viewModel)

    {

        var dialog = new ProductSearchDialog(viewModel);

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();

        return dialog.SelectedProducts;

    }



    public string? ShowReasonPromptDialog(string title, string message, string defaultReason = "")

    {

        var vm = new ReasonPromptDialogViewModel(title, message, defaultReason);

        var dialog = new ReasonPromptDialog(vm);

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();

        return dialog.ResultReason;

    }



    public ConfirmActionResult ShowConfirmActionDialog(string title, string message, bool isReferenced)

    {

        var dialog = new ConfirmActionDialog(title, message, isReferenced);

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();

        return dialog.Result;

    }



    public bool ShowBulkConfirmDialog(string actionName, int count, bool requireTypedConfirmation, string? detail = null)

    {

        var dialog = new BulkConfirmDialog(actionName, count, requireTypedConfirmation, detail);

        ConfigureDialogOwner(dialog);

        var res = dialog.ShowDialog();

        return res == true && dialog.Confirmed;

    }



    public bool ShowBulkSummaryDialog(string title, BulkOperationResult result)

    {

        var dialog = new BulkSummaryDialog(title, result);

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();

        return dialog.DeactivateRequested;

    }



    public bool? ShowStockAdjustmentDialog(StockAdjustmentDialogViewModel viewModel)

    {

        var dialog = new StockAdjustmentDialog(viewModel);

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    // Phase 5B Sales & POS Dialogs

    public bool? ShowSalePaymentDialog(SalePaymentDialogViewModel viewModel)

    {

        var dialog = new SalePaymentDialog { DataContext = viewModel };

        viewModel.RequestClose += () => dialog.DialogResult = viewModel.Confirmed;

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowSaleDetailDialog(SaleDetailDialogViewModel viewModel)

    {

        var dialog = new SaleDetailDialog { DataContext = viewModel };

        viewModel.RequestClose += () => dialog.Close();

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowSaleReturnDialog(SaleReturnDialogViewModel viewModel)

    {

        var dialog = new SaleReturnDialog { DataContext = viewModel };

        viewModel.RequestClose += () => dialog.DialogResult = viewModel.Confirmed;

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowHeldBillsDialog(HeldBillsDialogViewModel viewModel)

    {

        var dialog = new HeldBillsDialog { DataContext = viewModel };

        viewModel.RequestClose += () => dialog.Close();

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowBillDiscountDialog(BillDiscountDialogViewModel viewModel)

    {

        var dialog = new BillDiscountDialog { DataContext = viewModel };

        viewModel.RequestClose += () => dialog.DialogResult = viewModel.Confirmed;

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowLineDiscountDialog(LineDiscountDialogViewModel viewModel)

    {

        var dialog = new LineDiscountDialog { DataContext = viewModel };

        viewModel.RequestClose += () => dialog.DialogResult = viewModel.Confirmed;

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public void ShowPosHelpDialog(PosHelpDialogViewModel viewModel)

    {

        var dialog = new PosHelpDialog { DataContext = viewModel };

        viewModel.RequestClose += () => dialog.Close();

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();

    }



    public CartExitAction ShowCartExitPrompt()

    {

        var vm = new CartExitPromptDialogViewModel();

        var dialog = new CartExitPromptDialog { DataContext = vm };

        vm.RequestClose += () => dialog.Close();

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();

        return vm.SelectedAction;

    }



    public void ShowReceiptPreviewDialog(SalePaymentDialogViewModel? unused, ReceiptPreviewDialogViewModel viewModel)

    {

        ShowReceiptPreviewDialog(viewModel);

    }



    public void ShowReceiptPreviewDialog(ReceiptPreviewDialogViewModel viewModel)

    {

        var dialog = new ReceiptPreviewDialog { DataContext = viewModel };

        viewModel.RequestClose += () => dialog.Close();

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();



    }







    public void ShowReportPreviewDialog(ViewModels.Reports.ReportPreviewDialogViewModel viewModel)



    {



        var dialog = new Views.Dialogs.ReportPreviewDialog { DataContext = viewModel };



        viewModel.RequestClose += () => dialog.Close();



        ConfigureDialogOwner(dialog);



        dialog.ShowDialog();



    }







    public bool? ShowPrinterSettingsDialog(ViewModels.Settings.PrinterSettingsDialogViewModel viewModel)

    {

        var dialog = new PrinterSettingsDialog { DataContext = viewModel };

        viewModel.RequestClose += () => dialog.DialogResult = viewModel.Confirmed;

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public void ShowMessage(string message, string title = "Information")

    {

        var owner = GetSafeOwnerWindow();

        if (owner != null)

        {

            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

        }

        else

        {

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

        }

    }



    public bool ShowConfirmation(string message, string title = "Confirm")

    {

        var owner = GetSafeOwnerWindow();

        var res = owner != null

            ? MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)

            : MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);



        return res == MessageBoxResult.Yes;

    }



    public string? ShowOpenFileDialog(string filter, string title = "Open File")

    {

        var dialog = new OpenFileDialog

        {

            Filter = filter,

            Title = title

        };



        var owner = GetSafeOwnerWindow();

        bool? result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();

        return result == true ? dialog.FileName : null;

    }



    public string? ShowSaveFileDialog(string filter, string defaultFileName, string title = "Save File")

    {

        var dialog = new SaveFileDialog

        {

            Filter = filter,

            FileName = defaultFileName,

            Title = title

        };



        var owner = GetSafeOwnerWindow();

        bool? result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();

        return result == true ? dialog.FileName : null;

    }



    public bool? ShowCustomDialog(Window window)

    {

        ConfigureDialogOwner(window);

        return window.ShowDialog();

    }



    public bool ShowDataResetConfirmDialog(ViewModels.Dialogs.DataResetConfirmDialogViewModel viewModel)

    {

        var dialog = new DataResetConfirmDialog(viewModel);

        ConfigureDialogOwner(dialog);

        var result = dialog.ShowDialog();

        return result == true && viewModel.DialogResult;

    }



    public void ShowDataToolResultDialog(ViewModels.Dialogs.DataToolResultDialogViewModel viewModel)

    {

        var dialog = new DataToolResultDialog(viewModel);

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();

    }



    public bool? ShowRestoreConfirmDialog(ViewModels.Dialogs.RestoreConfirmDialogViewModel viewModel)

    {

        var dialog = new Views.Dialogs.RestoreConfirmDialog { DataContext = viewModel };

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowBackupPasswordDialog(ViewModels.Dialogs.BackupPasswordDialogViewModel viewModel)

    {

        var dialog = new Views.Dialogs.BackupPasswordDialog { DataContext = viewModel };

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowAppCloseBackupDialog(ViewModels.Dialogs.AppCloseBackupDialogViewModel viewModel)

    {

        var dialog = new Views.Dialogs.AppCloseBackupDialog { DataContext = viewModel };

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public string? ShowFolderBrowserDialog(string? description = null, string? initialPath = null)

    {

        var dialog = new Microsoft.Win32.OpenFolderDialog

        {

            Title = description ?? "Select Folder"

        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))

        {

            dialog.InitialDirectory = initialPath;

        }



        var owner = GetSafeOwnerWindow();

        bool? result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();

        return result == true ? dialog.FolderName : null;

    }



    public bool? ShowDriveConnectProgressDialog(ViewModels.Dialogs.DriveConnectProgressDialogViewModel viewModel)

    {

        var dialog = new Views.Dialogs.DriveConnectProgressDialog { DataContext = viewModel };

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowGitHubRestoreDialog(ViewModels.Dialogs.GitHubRestoreDialogViewModel viewModel)
    {
        var dialog = new Views.Dialogs.GitHubRestoreDialog { DataContext = viewModel };
        ConfigureDialogOwner(dialog);
        return dialog.ShowDialog();
    }

    public bool? ShowGitHubCopyRepoDialog(ViewModels.Dialogs.GitHubCopyRepoDialogViewModel viewModel)
    {
        var dialog = new Views.Dialogs.GitHubCopyRepoDialog { DataContext = viewModel };
        ConfigureDialogOwner(dialog);
        return dialog.ShowDialog();
    }

    public bool? ShowDriveBackupsDialog(ViewModels.Dialogs.DriveBackupsDialogViewModel viewModel)

    {

        var dialog = new Views.Dialogs.DriveBackupsDialog { DataContext = viewModel };

        ConfigureDialogOwner(dialog);

        return dialog.ShowDialog();

    }



    public bool? ShowSetupWizardDialog(ViewModels.Setup.SetupWizardViewModel viewModel)

    {

        var window = new Views.Setup.SetupWizardWindow(viewModel);

        return window.ShowDialog();

    }



    public void ShowUnhandledExceptionDialog(string message, string? stackTrace, string? logDirectory = null)

    {

        var dialog = new Views.Dialogs.UnhandledExceptionDialog(message, stackTrace, logDirectory);

        ConfigureDialogOwner(dialog);

        dialog.ShowDialog();

    }

}

