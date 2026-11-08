using System.IO;
using System;

using System.Collections.ObjectModel;

using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using ShopPOS.Core.DTOs.DataTools;

using ShopPOS.Core.Entities;

using ShopPOS.Core.Enums;

using ShopPOS.Core.Interfaces;

using ShopPOS.UI.Services;

using ShopPOS.UI.ViewModels.Dialogs;



namespace ShopPOS.UI.ViewModels.Settings;



public partial class SettingsViewModel : ObservableObject

{

    private readonly IShopSettingService _shopSettingService;

    private readonly IReceiptPrinterService? _receiptPrinterService;

    private readonly Services.IThemeService? _themeService;

    private readonly IDataToolsService? _dataToolsService;

    private readonly IDialogService? _dialogService;

    private readonly Services.INavigationService? _navigationService;

    private readonly ShopPOS.Core.Interfaces.IAppPaths? _appPaths;

    private readonly ShopPOS.Core.Interfaces.IUserSettingsService? _userSettingsService;

    private readonly ShopPOS.Core.Interfaces.IDiagnosticsService? _diagnosticsService;
    private readonly ShopPOS.Core.Interfaces.ISelfCheckService? _selfCheckService;

    private readonly ShopPOS.Core.Interfaces.IBackupService? _backupService;

    private ShopSetting _currentSetting = null!;



    // About & Diagnostics Properties

    [ObservableProperty]

    private string _appVersion = "1.0.0";



    [ObservableProperty]

    private string _buildDate = string.Empty;



    [ObservableProperty]

    private string _dataFolderPath = string.Empty;



    [ObservableProperty]

    private string _databaseSizeFormatted = "Unknown";



    [ObservableProperty]

    private string _schemaVersion = "Phase7B_GoogleDrive";



    [ObservableProperty]

    private string _backupStatusText = "No backups created yet";



    [ObservableProperty]

    [NotifyPropertyChangedFor(nameof(HasUpdateUrl))]

    private string? _updateInfoUrl;



    public bool HasUpdateUrl => !string.IsNullOrWhiteSpace(UpdateInfoUrl);



    [ObservableProperty]

    private string _selectedTheme = "Light";



    public ObservableCollection<string> AvailableThemes { get; } = new() { "Light", "Dark" };



    partial void OnSelectedThemeChanged(string value)

    {

        if (_themeService != null && _themeService.CurrentTheme != value)

        {

            _themeService.SetTheme(value);

        }

    }



    [RelayCommand]

    public void ToggleTheme()

    {

        if (_themeService != null)

        {

            _themeService.ToggleTheme();

            SelectedTheme = _themeService.CurrentTheme;

        }

    }



    [ObservableProperty]

    private string _shopName = "Shop POS";



    [ObservableProperty]

    private string? _address;



    [ObservableProperty]

    private string? _phone;



    [ObservableProperty]

    private string? _secondaryPhone;



    [ObservableProperty]

    private string? _logoImagePath;



    [ObservableProperty]

    private string? _storePolicy = "Items can be exchanged within 7 days with original receipt. No cash refunds.";



    [ObservableProperty]

    private string? _taxNumber;



    [ObservableProperty]

    private string _currencySymbol = "Rs";



    [ObservableProperty]

    private int _currencyDecimals = 2;



    public int[] AllowedDecimals { get; } = new[] { 0, 2, 3 };



    public string CurrencyPreview => $"{CurrencySymbol} 1,234.56";



    partial void OnCurrencySymbolChanged(string value) => OnPropertyChanged(nameof(CurrencyPreview));

    partial void OnCurrencyDecimalsChanged(int value) => OnPropertyChanged(nameof(CurrencyPreview));



    [ObservableProperty]

    private string? _receiptFooter;



    [ObservableProperty]

    private int _receiptWidthMm = 80;



    [ObservableProperty]

    private PrinterMode _printerMode = PrinterMode.EscPosRaw;



    [ObservableProperty]

    private decimal _defaultTaxPercent = 0m;



    [ObservableProperty]

    private CostingMethod _costingMethod = CostingMethod.WeightedAverage;



    [ObservableProperty]

    private bool _allowNegativeStock = false;



    [ObservableProperty]

    private bool _allowSellingBelowMinPrice = false;



    [ObservableProperty]

    private bool _pricesIncludeTax = true;



    [ObservableProperty]

    private decimal _cashRoundingStep = 0m;



    [ObservableProperty]

    private bool _allowReturnWithoutReceipt = false;



    [ObservableProperty]

    private bool _autoPrintReceipt = false;



    [ObservableProperty]

    private string? _receiptPrinterName;



    [ObservableProperty]

    private bool _openCashDrawerOnCashSale = false;



    [ObservableProperty]

    private int _receiptCopies = 1;



    [ObservableProperty]

    private int? _saleReturnWindowDays;



    [ObservableProperty]

    private bool _isAdmin = true;



    [ObservableProperty]

    private string? _errorMessage;



    [ObservableProperty]

    private string? _successMessage;



    [ObservableProperty]

    private bool _isLoading;



    // Data Tools Properties

    [ObservableProperty]

    private DataCountsDto? _dataCounts;



    [ObservableProperty]

    private bool _isDataToolsBusy;



    [ObservableProperty]

    private string? _dataToolsStatusMessage;



    public ObservableCollection<string> InstalledPrinters { get; } = new();



    public CostingMethod[] AvailableCostingMethods { get; } = (CostingMethod[])Enum.GetValues(typeof(CostingMethod));

    public PrinterMode[] AvailablePrinterModes { get; } = (PrinterMode[])Enum.GetValues(typeof(PrinterMode));



    public SettingsViewModel(

        IShopSettingService shopSettingService,

        IReceiptPrinterService? receiptPrinterService = null,

        Services.IThemeService? themeService = null,

        IDataToolsService? dataToolsService = null,

        IDialogService? dialogService = null,

        Services.INavigationService? navigationService = null,

        ShopPOS.Core.Interfaces.IAppPaths? appPaths = null,

        ShopPOS.Core.Interfaces.IUserSettingsService? userSettingsService = null,

        ShopPOS.Core.Interfaces.IDiagnosticsService? diagnosticsService = null,

        ShopPOS.Core.Interfaces.IBackupService? backupService = null,
        ShopPOS.Core.Interfaces.ISelfCheckService? selfCheckService = null)

    {

        _shopSettingService = shopSettingService;

        _receiptPrinterService = receiptPrinterService;

        _themeService = themeService;

        _dataToolsService = dataToolsService;

        _dialogService = dialogService;

        _navigationService = navigationService;

        _appPaths = appPaths;

        _userSettingsService = userSettingsService;

        _diagnosticsService = diagnosticsService;

        _backupService = backupService;
        _selfCheckService = selfCheckService;

        _selectedTheme = _themeService?.CurrentTheme ?? "Light";

    }



    public async Task InitializeAsync()

    {

        IsAdmin = true;

        IsLoading = true;

        try

        {

            _currentSetting = await _shopSettingService.GetSettingsAsync();



            ShopName = _currentSetting.ShopName;

            Address = _currentSetting.Address;

            Phone = _currentSetting.Phone;

            SecondaryPhone = _currentSetting.SecondaryPhone;

            LogoImagePath = _currentSetting.LogoImagePath;

            StorePolicy = _currentSetting.StorePolicy;

            TaxNumber = _currentSetting.TaxNumber;

            CurrencySymbol = _currentSetting.CurrencySymbol;

            CurrencyDecimals = _currentSetting.CurrencyDecimals;

            ReceiptFooter = _currentSetting.ReceiptFooter;

            ReceiptWidthMm = _currentSetting.ReceiptWidthMm > 0 ? _currentSetting.ReceiptWidthMm : 80;

            PrinterMode = _currentSetting.PrinterMode;

            DefaultTaxPercent = _currentSetting.DefaultTaxPercent;

            CostingMethod = _currentSetting.CostingMethod;

            AllowNegativeStock = _currentSetting.AllowNegativeStock;



            AllowSellingBelowMinPrice = _currentSetting.AllowSellingBelowMinPrice;

            PricesIncludeTax = _currentSetting.PricesIncludeTax;

            CashRoundingStep = _currentSetting.CashRoundingStep;

            AllowReturnWithoutReceipt = _currentSetting.AllowReturnWithoutReceipt;

            AutoPrintReceipt = _currentSetting.AutoPrintReceipt;

            ReceiptPrinterName = _currentSetting.ReceiptPrinterName;

            OpenCashDrawerOnCashSale = _currentSetting.OpenCashDrawerOnCashSale;

            ReceiptCopies = Math.Max(1, _currentSetting.ReceiptCopies);

            SaleReturnWindowDays = _currentSetting.SaleReturnWindowDays;



            await RefreshPrintersAsync();

            await RefreshDataCountsAsync();

            await RefreshAboutInfoAsync();

        }

        catch (Exception ex)

        {

            ErrorMessage = $"Error loading settings: {ex.Message}";

        }

        finally

        {

            IsLoading = false;

        }

    }



    [RelayCommand]

    public async Task RefreshPrintersAsync()

    {

        if (_receiptPrinterService == null) return;



        try

        {

            var printers = await _receiptPrinterService.GetInstalledPrintersAsync();

            InstalledPrinters.Clear();

            foreach (var printer in printers)

            {

                InstalledPrinters.Add(printer);

            }



            if (!string.IsNullOrWhiteSpace(ReceiptPrinterName) && !InstalledPrinters.Contains(ReceiptPrinterName))

            {

                InstalledPrinters.Insert(0, ReceiptPrinterName);

            }

            else if (string.IsNullOrWhiteSpace(ReceiptPrinterName) && InstalledPrinters.Count > 0)

            {

                ReceiptPrinterName = InstalledPrinters[0];

            }

        }

        catch (Exception ex)

        {

            ErrorMessage = $"Could not detect system printers: {ex.Message}";

        }

    }



    [RelayCommand]

    public void NavigateToBackupRestore()

    {

        _navigationService?.NavigateTo("BackupRestore");

    }



    [RelayCommand]

    public async Task RefreshDataCountsAsync()

    {

        if (_dataToolsService == null) return;

        try

        {

            DataCounts = await _dataToolsService.GetCountsAsync();

        }

        catch (Exception ex)

        {

            ErrorMessage = $"Could not load data statistics: {ex.Message}";

        }

    }



    [RelayCommand]

    public async Task ClearTransactionsAsync()

    {

        if (_dataToolsService == null || _dialogService == null) return;



        var counts = await _dataToolsService.GetCountsAsync();

        var vm = new DataResetConfirmDialogViewModel

        {

            Title = "Clear Transactions",

            ActionName = "Clear All Sales, Returns, Purchases, Payments, Adjustments, Expenses & Ledger",

            WarningMessage = "This permanently clears all transactional history, resets document sequences to 1, and empties the stock ledger. Products, categories, suppliers, and settings will remain completely intact."

        };



        foreach (var item in counts.GetTransactionCounts())

        {

            vm.TablesToDelete.Add(item);

        }



        vm.TablesToKeep.Add($"Products ({counts.ProductsCount})");

        vm.TablesToKeep.Add($"Categories ({counts.CategoriesCount})");

        vm.TablesToKeep.Add($"Brands ({counts.BrandsCount})");

        vm.TablesToKeep.Add($"Units ({counts.UnitsCount})");

        vm.TablesToKeep.Add($"Suppliers ({counts.SuppliersCount})");

        vm.TablesToKeep.Add("Store Profile & POS Settings");



        if (_dialogService.ShowDataResetConfirmDialog(vm))

        {

            IsDataToolsBusy = true;

            DataToolsStatusMessage = "Creating backup and clearing transactions...";

            try

            {

                var result = await _dataToolsService.ClearTransactionsAsync();

                if (result.Success)

                {

                    var resVm = new DataToolResultDialogViewModel

                    {

                        Title = "Transactions Cleared Successfully",

                        Message = result.Message,

                        BackupFilePath = result.BackupFilePath,

                        IsDemoData = false

                    };

                    foreach (var d in result.DeletedRows) resVm.DeletedRows.Add(d);

                    _dialogService.ShowDataToolResultDialog(resVm);

                    await RefreshDataCountsAsync();

                }

                else

                {

                    _dialogService.ShowMessage(result.Error ?? "Failed to clear transactions.", "Error");

                }

            }

            catch (Exception ex)

            {

                _dialogService.ShowMessage($"Error: {ex.Message}", "Error");

            }

            finally

            {

                IsDataToolsBusy = false;

                DataToolsStatusMessage = null;

            }

        }

    }



    [RelayCommand]

    public async Task ClearEverythingAsync()

    {

        if (_dataToolsService == null || _dialogService == null) return;



        var counts = await _dataToolsService.GetCountsAsync();

        var vm = new DataResetConfirmDialogViewModel

        {

            Title = "Clear Everything (New Shop)",

            ActionName = "Reset entire database to start a clean new shop from zero",

            WarningMessage = "This will wipe all transactions, products, categories, brands, units, suppliers, and expenses. It will re-seed default units, General category, Retail & Wholesale price levels, and default expense categories. Settings and the owner user row will be preserved."

        };



        foreach (var item in counts.GetTransactionCounts()) vm.TablesToDelete.Add(item);

        foreach (var item in counts.GetMasterDataCounts()) vm.TablesToDelete.Add(item);



        vm.TablesToKeep.Add("Store Profile & POS Settings");

        vm.TablesToKeep.Add("Local Owner User Account");

        vm.TablesToKeep.Add("Default Master Data (Re-seeded)");



        if (_dialogService.ShowDataResetConfirmDialog(vm))

        {

            IsDataToolsBusy = true;

            DataToolsStatusMessage = "Creating backup and resetting entire database...";

            try

            {

                var result = await _dataToolsService.ClearEverythingAsync();

                if (result.Success)

                {

                    var resVm = new DataToolResultDialogViewModel

                    {

                        Title = "Fresh Shop Reset Completed",

                        Message = result.Message,

                        BackupFilePath = result.BackupFilePath,

                        IsDemoData = false

                    };

                    foreach (var d in result.DeletedRows) resVm.DeletedRows.Add(d);

                    _dialogService.ShowDataToolResultDialog(resVm);

                    await RefreshDataCountsAsync();

                }

                else

                {

                    _dialogService.ShowMessage(result.Error ?? "Failed to reset shop.", "Error");

                }

            }

            catch (Exception ex)

            {

                _dialogService.ShowMessage($"Error: {ex.Message}", "Error");

            }

            finally

            {

                IsDataToolsBusy = false;

                DataToolsStatusMessage = null;

            }

        }

    }



    [RelayCommand]

    public async Task ClearAuditLogAsync()

    {

        if (_dataToolsService == null || _dialogService == null) return;



        var counts = await _dataToolsService.GetCountsAsync();

        var vm = new DataResetConfirmDialogViewModel

        {

            Title = "Clear Audit Log",

            ActionName = "Clear system audit logs",

            WarningMessage = "This will clear all audit logs. All products, inventory, transactions, and settings will remain completely untouched."

        };



        vm.TablesToDelete.Add(new TableCountDto { TableName = "Audit Logs", Count = counts.AuditLogsCount });

        vm.TablesToKeep.Add("All Transactions");

        vm.TablesToKeep.Add("All Master Data");

        vm.TablesToKeep.Add("Settings");



        if (_dialogService.ShowDataResetConfirmDialog(vm))

        {

            IsDataToolsBusy = true;

            DataToolsStatusMessage = "Creating backup and clearing audit log...";

            try

            {

                var result = await _dataToolsService.ClearAuditLogOnlyAsync();

                if (result.Success)

                {

                    var resVm = new DataToolResultDialogViewModel

                    {

                        Title = "Audit Log Cleared",

                        Message = result.Message,

                        BackupFilePath = result.BackupFilePath,

                        IsDemoData = false

                    };

                    _dialogService.ShowDataToolResultDialog(resVm);

                    await RefreshDataCountsAsync();

                }

                else

                {

                    _dialogService.ShowMessage(result.Error ?? "Failed to clear audit log.", "Error");

                }

            }

            catch (Exception ex)

            {

                _dialogService.ShowMessage($"Error: {ex.Message}", "Error");

            }

            finally

            {

                IsDataToolsBusy = false;

                DataToolsStatusMessage = null;

            }

        }

    }



    [RelayCommand]

    public async Task LoadDemoDataAsync()

    {

        if (_dataToolsService == null || _dialogService == null) return;



        var counts = await _dataToolsService.GetCountsAsync();

        if (counts.TotalTransactionsCount > 0)

        {

            _dialogService.ShowMessage("Transaction tables are not empty. Please run 'Clear transactions' first before loading demo data.", "Transactions Exist");

            return;

        }



        bool confirm = _dialogService.ShowConfirmation(

            "Load realistic retail demo data? This will create 6 categories, 6 brands, 5 suppliers, ~60 products with valid EAN-13 barcodes, 8 purchases, 40 sales, and 25 expenses. An automatic backup will be created first.",

            "Confirm Load Demo Data");



        if (!confirm) return;



        IsDataToolsBusy = true;

        DataToolsStatusMessage = "Loading demo data (generating products, barcodes, purchases, sales)...";

        var progress = new Progress<string>(msg => DataToolsStatusMessage = msg);



        try

        {

            var result = await Task.Run(() => _dataToolsService.LoadDemoDataAsync(null, progress));

            if (result.Success)

            {

                var resVm = new DataToolResultDialogViewModel

                {

                    Title = "Demo Shop Data Loaded",

                    Message = result.Message,

                    BackupFilePath = result.BackupFilePath,

                    IsDemoData = true,

                    CategoriesCreated = result.CategoriesCreated,

                    BrandsCreated = result.BrandsCreated,

                    SuppliersCreated = result.SuppliersCreated,

                    ProductsCreated = result.ProductsCreated,

                    PurchasesCreated = result.PurchasesCreated,

                    SalesCreated = result.SalesCreated,

                    ReturnsCreated = result.ReturnsCreated,

                    ExpensesCreated = result.ExpensesCreated

                };

                foreach (var b in result.SampleBarcodes) resVm.SampleBarcodes.Add(b);

                _dialogService.ShowDataToolResultDialog(resVm);

                await RefreshDataCountsAsync();

            }

            else

            {

                _dialogService.ShowMessage(result.Error ?? "Failed to load demo data.", "Error");

            }

        }

        catch (Exception ex)

        {

            _dialogService.ShowMessage($"Error: {ex.Message}", "Error");

        }

        finally

        {

            IsDataToolsBusy = false;

            DataToolsStatusMessage = null;

        }

    }



    [RelayCommand]

    public async Task TestPrintAsync()

    {

        if (_receiptPrinterService == null)

        {

            ErrorMessage = "Receipt printer service not available.";

            return;

        }



        try

        {

            var testSetting = new ShopSetting

            {

                ShopName = ShopName,

                Address = Address,

                Phone = Phone,

                SecondaryPhone = SecondaryPhone,

                LogoImagePath = LogoImagePath,

                StorePolicy = StorePolicy,

                TaxNumber = TaxNumber,

                CurrencySymbol = CurrencySymbol,

                CurrencyDecimals = CurrencyDecimals,

                ReceiptFooter = ReceiptFooter,

                ReceiptWidthMm = ReceiptWidthMm,

                PrinterMode = PrinterMode,

                ReceiptPrinterName = ReceiptPrinterName?.Trim(),

                AutoPrintReceipt = AutoPrintReceipt,

                OpenCashDrawerOnCashSale = OpenCashDrawerOnCashSale,

                ReceiptCopies = 1

            };



            var result = await _receiptPrinterService.PrintTestReceiptAsync(testSetting);

            if (result.Success)

            {

                SuccessMessage = result.NoticeMessage ?? $"Test print sent successfully to '{ReceiptPrinterName}'.";

            }

            else

            {

                ErrorMessage = result.ErrorMessage ?? "Test print failed. Please check printer connection.";

            }

        }

        catch (Exception ex)

        {

            ErrorMessage = $"Test print failed: {ex.Message}";

        }

    }



    [RelayCommand]

    public async Task OpenCashDrawerAsync()

    {

        if (_receiptPrinterService == null)

        {

            ErrorMessage = "Receipt printer service not available.";

            return;

        }



        try

        {

            await _receiptPrinterService.OpenCashDrawerAsync(ReceiptPrinterName);

            SuccessMessage = $"Cash drawer kick pulse sent to '{ReceiptPrinterName}'.";

        }

        catch (Exception ex)

        {

            ErrorMessage = $"Cash drawer kick failed: {ex.Message}";

        }

    }



    [RelayCommand]

    public void SelectLogoImage()

    {

        var ofd = new Microsoft.Win32.OpenFileDialog

        {

            Title = "Select Store Logo for Thermal Receipts",

            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*"

        };



        if (ofd.ShowDialog() == true)

        {

            try

            {

                string targetDir = @"C:\ShopData";

                if (!System.IO.Directory.Exists(targetDir))

                {

                    System.IO.Directory.CreateDirectory(targetDir);

                }



                string ext = System.IO.Path.GetExtension(ofd.FileName);

                string targetPath = System.IO.Path.Combine(targetDir, $"StoreLogo_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");

                System.IO.File.Copy(ofd.FileName, targetPath, overwrite: true);

                LogoImagePath = targetPath;

            }

            catch (Exception ex)

            {

                ErrorMessage = $"Could not copy logo image: {ex.Message}";

            }

        }

    }



    [RelayCommand]

    public void RemoveLogoImage()

    {

        LogoImagePath = null;

    }



    [RelayCommand]

    public async Task SaveAsync()

    {

        ErrorMessage = null;

        SuccessMessage = null;



        if (string.IsNullOrWhiteSpace(ShopName))

        {

            ErrorMessage = "Store name is required.";

            return;

        }



        IsLoading = true;

        try

        {

            _currentSetting.ShopName = ShopName.Trim();

            _currentSetting.Address = Address?.Trim();

            _currentSetting.Phone = Phone?.Trim();

            _currentSetting.SecondaryPhone = SecondaryPhone?.Trim();

            _currentSetting.LogoImagePath = LogoImagePath?.Trim();

            _currentSetting.StorePolicy = StorePolicy?.Trim();

            _currentSetting.TaxNumber = TaxNumber?.Trim();

            _currentSetting.CurrencySymbol = string.IsNullOrWhiteSpace(CurrencySymbol) ? "Rs" : CurrencySymbol.Trim();

            _currentSetting.CurrencyDecimals = Math.Clamp(CurrencyDecimals, 0, 3);

            ReceiptFooter = ReceiptFooter?.Trim();

            _currentSetting.ReceiptFooter = ReceiptFooter;

            _currentSetting.ReceiptWidthMm = ReceiptWidthMm;

            _currentSetting.PrinterMode = PrinterMode;

            _currentSetting.DefaultTaxPercent = DefaultTaxPercent;

            _currentSetting.CostingMethod = CostingMethod;

            _currentSetting.AllowNegativeStock = AllowNegativeStock;



            _currentSetting.AllowSellingBelowMinPrice = AllowSellingBelowMinPrice;

            _currentSetting.PricesIncludeTax = PricesIncludeTax;

            _currentSetting.CashRoundingStep = CashRoundingStep;

            _currentSetting.AllowReturnWithoutReceipt = AllowReturnWithoutReceipt;

            _currentSetting.AutoPrintReceipt = AutoPrintReceipt;

            _currentSetting.ReceiptPrinterName = ReceiptPrinterName?.Trim();

            _currentSetting.OpenCashDrawerOnCashSale = OpenCashDrawerOnCashSale;

            _currentSetting.ReceiptCopies = Math.Max(1, ReceiptCopies);

            _currentSetting.SaleReturnWindowDays = SaleReturnWindowDays;



            await _shopSettingService.UpdateSettingsAsync(_currentSetting);

            ShopPOS.Core.Services.MoneyFormatter.Instance.UpdateSettings(_currentSetting.CurrencySymbol, _currentSetting.CurrencyDecimals);

            SuccessMessage = "Settings saved successfully.";

        }

        catch (Exception ex)

        {

            ErrorMessage = $"Error saving settings: {ex.Message}";

        }

        finally

        {

            IsLoading = false;

        }

    }



    [RelayCommand]

    public async Task RefreshAboutInfoAsync()

    {

        try

        {

            AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

            BuildDate = DateTime.Now.ToString("dd MMM yyyy");

            DataFolderPath = _appPaths?.DataDirectory ?? string.Empty;



            if (_appPaths != null && File.Exists(_appPaths.DatabasePath))

            {

                long bytes = new FileInfo(_appPaths.DatabasePath).Length;

                DatabaseSizeFormatted = bytes < 1024 * 1024

                    ? $"{(bytes / 1024.0):F1} KB"

                    : $"{(bytes / (1024.0 * 1024.0)):F2} MB";

            }



            if (_backupService != null)

            {

                var summary = await _backupService.GetBackupSummaryAsync();

                BackupStatusText = summary.LastBackupAt.HasValue

                    ? $"Last: {summary.LastBackupAtFormatted} ({summary.TotalBackupsCount} total)"

                    : "No backups created yet";

            }



            if (_userSettingsService != null)

            {

                var us = _userSettingsService.Load();

                UpdateInfoUrl = us.UpdateInfoUrl;

            }

        }

        catch { }

    }



    [RelayCommand]

    public void OpenDataFolder()

    {

        string dir = _appPaths?.DataDirectory ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))

        {

            try

            {

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo

                {

                    FileName = dir,

                    UseShellExecute = true

                });

            }

            catch { }

        }

    }



    [RelayCommand]

    public void OpenLogsFolder()

    {

        string dir = _appPaths?.LogsDirectory ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))

        {

            try

            {

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo

                {

                    FileName = dir,

                    UseShellExecute = true

                });

            }

            catch { }

        }

    }



    [RelayCommand]

    public void CheckForUpdates()

    {

        if (!string.IsNullOrWhiteSpace(UpdateInfoUrl))

        {

            try

            {

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo

                {

                    FileName = UpdateInfoUrl,

                    UseShellExecute = true

                });

            }

            catch { }

        }

    }



        [RelayCommand]
    public async Task RunSelfCheckAsync()
    {
        if (_selfCheckService == null)
        {
            _dialogService?.ShowMessage("Self check service is not available.", "Error");
            return;
        }

        IsLoading = true;
        try
        {
            var report = await _selfCheckService.RunAllChecksAsync();
            string title = report.AllPassed ? "Self Check: All Passed" : "Self Check: Issues Detected";
            _dialogService?.ShowMessage(report.FormattedReport, title);
        }
        catch (Exception ex)
        {
            _dialogService?.ShowMessage($"Self check failed with error: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

[RelayCommand]

    public async Task CreateSupportBundleAsync()

    {

        if (_diagnosticsService == null)

        {

            _dialogService?.ShowMessage("Diagnostics service is not available.", "Error");

            return;

        }



        string defaultFileName = $"ShopPOS_Support_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

        string? targetPath = _dialogService?.ShowSaveFileDialog("ZIP Archive (*.zip)|*.zip", defaultFileName, "Save Support Diagnostics Bundle");

        if (string.IsNullOrWhiteSpace(targetPath)) return;



        IsLoading = true;

        try

        {

            await _diagnosticsService.CreateSupportBundleAsync(targetPath);

            _dialogService?.ShowMessage($"Support file created successfully! Location: {targetPath} (Contains sanitized system information and recent log files. Database and passwords are never included.)", "Support File Created");

        }

        catch (Exception ex)

        {

            _dialogService?.ShowMessage($"Failed to generate support file: {ex.Message}", "Error");

        }

        finally

        {

            IsLoading = false;

        }

    }

}

