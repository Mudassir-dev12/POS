using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Settings;

public partial class PrinterSettingsDialogViewModel : ObservableObject
{
    private readonly IShopSettingService _shopSettingService;
    private readonly IReceiptPrinterService _receiptPrinterService;
    private ShopSetting _currentSetting = new();

    [ObservableProperty]
    private PrinterMode _printerMode = PrinterMode.PdfPreview;

    [ObservableProperty]
    private string? _selectedPrinter;

    [ObservableProperty]
    private int _receiptWidthMm = 80;

    [ObservableProperty]
    private int _receiptCopies = 1;

    [ObservableProperty]
    private bool _autoPrintReceipt = true;

    [ObservableProperty]
    private bool _openCashDrawerOnCashSale = false;

    [ObservableProperty]
    private string? _receiptFooter = "Thank you for your business!";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<string> InstalledPrinters { get; } = new();

    public PrinterMode[] AvailablePrinterModes { get; } = (PrinterMode[])Enum.GetValues(typeof(PrinterMode));

    public bool Confirmed { get; private set; }

    public event Action? RequestClose;

    public PrinterSettingsDialogViewModel(
        IShopSettingService shopSettingService,
        IReceiptPrinterService receiptPrinterService)
    {
        _shopSettingService = shopSettingService;
        _receiptPrinterService = receiptPrinterService;

        _ = InitializeAsync();
    }

    // Parameterless constructor for DI and smoke tests
    public PrinterSettingsDialogViewModel()
    {
        _shopSettingService = null!;
        _receiptPrinterService = null!;
    }

    public async Task InitializeAsync()
    {
        if (_shopSettingService == null) return;

        IsLoading = true;
        try
        {
            _currentSetting = await _shopSettingService.GetSettingsAsync();

            PrinterMode = _currentSetting.PrinterMode;
            SelectedPrinter = _currentSetting.ReceiptPrinterName;
            ReceiptWidthMm = _currentSetting.ReceiptWidthMm > 0 ? _currentSetting.ReceiptWidthMm : 80;
            ReceiptCopies = Math.Max(1, _currentSetting.ReceiptCopies);
            AutoPrintReceipt = _currentSetting.AutoPrintReceipt;
            OpenCashDrawerOnCashSale = _currentSetting.OpenCashDrawerOnCashSale;
            ReceiptFooter = _currentSetting.ReceiptFooter;

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

            if (!string.IsNullOrWhiteSpace(SelectedPrinter) && !InstalledPrinters.Contains(SelectedPrinter))
            {
                InstalledPrinters.Insert(0, SelectedPrinter);
            }
            else if (string.IsNullOrWhiteSpace(SelectedPrinter) && InstalledPrinters.Count > 0)
            {
                SelectedPrinter = InstalledPrinters[0];
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not detect installed printers: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task TestPrintAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (_receiptPrinterService == null) return;

        var testSetting = new ShopSetting
        {
            ShopName = _currentSetting.ShopName,
            Address = _currentSetting.Address,
            Phone = _currentSetting.Phone,
            TaxNumber = _currentSetting.TaxNumber,
            CurrencySymbol = _currentSetting.CurrencySymbol,
            ReceiptFooter = ReceiptFooter,
            ReceiptWidthMm = ReceiptWidthMm,
            PrinterMode = PrinterMode,
            ReceiptPrinterName = SelectedPrinter?.Trim(),
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
                StatusMessage = result.NoticeMessage ?? "Test print successfully sent!";
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
        StatusMessage = null;

        if (_receiptPrinterService == null) return;

        IsLoading = true;
        try
        {
            var result = await _receiptPrinterService.OpenCashDrawerAsync(SelectedPrinter?.Trim());
            if (result.Success)
            {
                StatusMessage = result.NoticeMessage ?? "Cash drawer kick pulse sent!";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Could not trigger cash drawer. Verify printer connection.";
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
        StatusMessage = null;

        if (_shopSettingService == null) return;

        IsLoading = true;
        try
        {
            _currentSetting.PrinterMode = PrinterMode;
            _currentSetting.ReceiptPrinterName = SelectedPrinter?.Trim();
            _currentSetting.ReceiptWidthMm = ReceiptWidthMm;
            _currentSetting.ReceiptCopies = Math.Max(1, ReceiptCopies);
            _currentSetting.AutoPrintReceipt = AutoPrintReceipt;
            _currentSetting.OpenCashDrawerOnCashSale = OpenCashDrawerOnCashSale;
            _currentSetting.ReceiptFooter = ReceiptFooter?.Trim();

            await _shopSettingService.UpdateSettingsAsync(_currentSetting);

            Confirmed = true;
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save printer settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        Confirmed = false;
        RequestClose?.Invoke();
    }
}
