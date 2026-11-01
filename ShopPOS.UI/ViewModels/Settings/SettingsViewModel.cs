using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Settings;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IShopSettingService _shopSettingService;
    private readonly IAuthService _authService;
    private readonly IPermissionService _permissionService;
    private readonly IReceiptPrinterService? _receiptPrinterService;
    private ShopSetting _currentSetting = null!;

    [ObservableProperty]
    private string _shopName = "Shop POS";

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _taxNumber;

    [ObservableProperty]
    private string _currencySymbol = "$";

    [ObservableProperty]
    private string? _receiptFooter;

    [ObservableProperty]
    private int _receiptWidthMm = 80;

    [ObservableProperty]
    private PrinterMode _printerMode = PrinterMode.PdfPreview;

    [ObservableProperty]
    private decimal _defaultTaxPercent;

    [ObservableProperty]
    private CostingMethod _costingMethod = CostingMethod.WeightedAverage;

    [ObservableProperty]
    private bool _allowNegativeStock;

    // Phase 5 Sales & POS Settings
    [ObservableProperty]
    private bool _requireOpenShiftForSales = true;

    [ObservableProperty]
    private bool _allowCreditSales = true;

    [ObservableProperty]
    private bool _allowSellingBelowMinPrice;

    [ObservableProperty]
    private bool _pricesIncludeTax;

    [ObservableProperty]
    private decimal _cashRoundingStep;

    [ObservableProperty]
    private bool _allowReturnWithoutReceipt;

    [ObservableProperty]
    private bool _autoPrintReceipt = true;

    [ObservableProperty]
    private string? _receiptPrinterName;

    [ObservableProperty]
    private bool _openCashDrawerOnCashSale;

    [ObservableProperty]
    private int _receiptCopies = 1;

    [ObservableProperty]
    private int? _saleReturnWindowDays;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<string> InstalledPrinters { get; } = new();

    public CostingMethod[] AvailableCostingMethods { get; } = (CostingMethod[])Enum.GetValues(typeof(CostingMethod));
    public PrinterMode[] AvailablePrinterModes { get; } = (PrinterMode[])Enum.GetValues(typeof(PrinterMode));

    public SettingsViewModel(
        IShopSettingService shopSettingService,
        IAuthService authService,
        IPermissionService permissionService,
        IReceiptPrinterService? receiptPrinterService = null)
    {
        _shopSettingService = shopSettingService;
        _authService = authService;
        _permissionService = permissionService;
        _receiptPrinterService = receiptPrinterService;
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
            TaxNumber = _currentSetting.TaxNumber;
            CurrencySymbol = _currentSetting.CurrencySymbol;
            ReceiptFooter = _currentSetting.ReceiptFooter;
            ReceiptWidthMm = _currentSetting.ReceiptWidthMm > 0 ? _currentSetting.ReceiptWidthMm : 80;
            PrinterMode = _currentSetting.PrinterMode;
            DefaultTaxPercent = _currentSetting.DefaultTaxPercent;
            CostingMethod = _currentSetting.CostingMethod;
            AllowNegativeStock = _currentSetting.AllowNegativeStock;

            // Phase 5
            RequireOpenShiftForSales = _currentSetting.RequireOpenShiftForSales;
            AllowCreditSales = _currentSetting.AllowCreditSales;
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
            foreach (var p in printers)
            {
                InstalledPrinters.Add(p);
            }

            if (!string.IsNullOrWhiteSpace(ReceiptPrinterName) && !InstalledPrinters.Contains(ReceiptPrinterName))
            {
                InstalledPrinters.Insert(0, ReceiptPrinterName);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load printer list: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task TestPrintAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (_receiptPrinterService == null) return;

        var testSetting = new ShopSetting
        {
            ShopName = ShopName,
            Address = Address,
            Phone = Phone,
            TaxNumber = TaxNumber,
            CurrencySymbol = CurrencySymbol,
            ReceiptFooter = ReceiptFooter,
            ReceiptWidthMm = ReceiptWidthMm,
            PrinterMode = PrinterMode,
            ReceiptPrinterName = ReceiptPrinterName?.Trim(),
            AutoPrintReceipt = AutoPrintReceipt,
            OpenCashDrawerOnCashSale = OpenCashDrawerOnCashSale,
            ReceiptCopies = 1
        };

        IsLoading = true;
        try
        {
            var result = await _receiptPrinterService.PrintTestReceiptAsync(testSetting);
            if (result.Success)
            {
                SuccessMessage = result.NoticeMessage ?? "Test print sent successfully.";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Test print failed. Please check printer connection.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Printer error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task OpenCashDrawerAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (_receiptPrinterService == null) return;

        IsLoading = true;
        try
        {
            var result = await _receiptPrinterService.OpenCashDrawerAsync(ReceiptPrinterName?.Trim());
            if (result.Success)
            {
                SuccessMessage = result.NoticeMessage ?? "Cash drawer kick pulse sent!";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Could not send cash drawer kick pulse.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Cash drawer error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
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
            _currentSetting.TaxNumber = TaxNumber?.Trim();
            _currentSetting.CurrencySymbol = string.IsNullOrWhiteSpace(CurrencySymbol) ? "$" : CurrencySymbol.Trim();
            ReceiptFooter = ReceiptFooter?.Trim();
            _currentSetting.ReceiptFooter = ReceiptFooter;
            _currentSetting.ReceiptWidthMm = ReceiptWidthMm;
            _currentSetting.PrinterMode = PrinterMode;
            _currentSetting.DefaultTaxPercent = DefaultTaxPercent;
            _currentSetting.CostingMethod = CostingMethod;
            _currentSetting.AllowNegativeStock = AllowNegativeStock;

            // Phase 5
            _currentSetting.RequireOpenShiftForSales = RequireOpenShiftForSales;
            _currentSetting.AllowCreditSales = AllowCreditSales;
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
}
