using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.UI.Services;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class SaleDetailDialogViewModel : ObservableObject
{
    private readonly ISaleService _saleService;
    private readonly IPdfReportService _pdfReportService;
    private readonly IShopSettingService _shopSettingService;
    private readonly IReceiptPrinterService? _receiptPrinterService;
    private readonly IReceiptBuilder? _receiptBuilder;
    private readonly IAuditLogService? _auditLogService;
    private readonly ICurrentUserProvider? _currentUserProvider;
    private readonly IDialogService? _dialogService;

    [ObservableProperty]
    private SaleDetailDto? _sale;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _currencySymbol = "Rs";

    public event Action? RequestClose;

    public SaleDetailDialogViewModel(
        int saleId,
        ISaleService saleService,
        IPdfReportService pdfReportService,
        IShopSettingService shopSettingService,
        IReceiptPrinterService? receiptPrinterService = null,
        IReceiptBuilder? receiptBuilder = null,
        IAuditLogService? auditLogService = null,
        ICurrentUserProvider? currentUserProvider = null,
        IDialogService? dialogService = null)
    {
        _saleService = saleService;
        _pdfReportService = pdfReportService;
        _shopSettingService = shopSettingService;
        _receiptPrinterService = receiptPrinterService;
        _receiptBuilder = receiptBuilder;
        _auditLogService = auditLogService;
        _currentUserProvider = currentUserProvider;
        _dialogService = dialogService;

        _ = LoadSaleAsync(saleId);
    }

    public async Task LoadSaleAsync(int saleId)
    {
        IsLoading = true;
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            CurrencySymbol = settings.CurrencySymbol;

            var result = await _saleService.GetSaleByIdAsync(saleId);
            if (result.Success && result.Data != null)
            {
                Sale = result.Data;
            }
            else
            {
                ErrorMessage = result.Error ?? "Sale not found.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ReprintReceiptAsync()
    {
        StatusMessage = null;
        if (Sale == null || _receiptPrinterService == null || _shopSettingService == null) return;

        IsLoading = true;
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();

            // 1. Audit Log reprint
            if (_auditLogService != null && _currentUserProvider != null)
            {
                await _auditLogService.LogAsync(
                    _currentUserProvider.UserId,
                    "ReprintReceipt",
                    "Sale",
                    Sale.Id,
                    $"Reprinted receipt for sale {Sale.SaleNumber} (marked DUPLICATE)");
            }

            // 2. Print duplicate receipt
            var result = await _receiptPrinterService.PrintSaleReceiptAsync(Sale, settings, isDuplicate: true);

            if (result.Success)
            {
                if (settings.PrinterMode == Core.Enums.PrinterMode.PdfPreview && result.PdfBytes != null && _receiptBuilder != null && _dialogService != null)
                {
                    var doc = _receiptBuilder.BuildFromSale(Sale, settings, isDuplicate: true);
                    var previewVm = new ReceiptPreviewDialogViewModel(doc, result.PdfBytes, _pdfReportService, _receiptPrinterService, _shopSettingService);
                    _dialogService.ShowReceiptPreviewDialog(previewVm);
                }
                else
                {
                    StatusMessage = result.NoticeMessage ?? "Duplicate receipt sent to printer.";
                }
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Could not print duplicate receipt.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reprint error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PrintA4InvoiceAsync()
    {
        StatusMessage = null;
        if (Sale == null || _receiptBuilder == null || _pdfReportService == null || _shopSettingService == null || _dialogService == null) return;

        IsLoading = true;
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            var doc = _receiptBuilder.BuildFromSale(Sale, settings, isDuplicate: false);
            doc.PaperWidthMm = 210; // A4

            byte[] pdfBytes = await _pdfReportService.GenerateA4InvoicePdfAsync(doc);
            var previewVm = new ReceiptPreviewDialogViewModel(doc, pdfBytes, _pdfReportService, _receiptPrinterService ?? null!, _shopSettingService);
            _dialogService.ShowReceiptPreviewDialog(previewVm);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not generate A4 invoice: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }
}
