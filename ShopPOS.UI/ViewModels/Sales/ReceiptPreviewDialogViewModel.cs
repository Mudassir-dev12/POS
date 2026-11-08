using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.UI.ViewModels.Sales;

public partial class ReceiptPreviewDialogViewModel : ObservableObject
{
    private readonly IPdfReportService _pdfReportService;
    private readonly IReceiptPrinterService _receiptPrinterService;
    private readonly IShopSettingService _shopSettingService;

    [ObservableProperty]
    private string _title = "Receipt Preview";

    [ObservableProperty]
    private string _documentTitle = "SALES RECEIPT";

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private byte[]? _pdfBytes;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _selectedPrinter;

    public ObservableCollection<string> AvailablePrinters { get; } = new();

    public ObservableCollection<BitmapSource> PageImages { get; } = new();

    public ReceiptDocument? Document { get; private set; }

    public event Action? RequestClose;

    public ReceiptPreviewDialogViewModel(
        ReceiptDocument doc,
        byte[] pdfBytes,
        IPdfReportService pdfReportService,
        IReceiptPrinterService receiptPrinterService,
        IShopSettingService shopSettingService)
    {
        Document = doc;
        _pdfBytes = pdfBytes;
        _pdfReportService = pdfReportService;
        _receiptPrinterService = receiptPrinterService;
        _shopSettingService = shopSettingService;

        Title = $"{doc.DocumentTitle} - {doc.InvoiceNumber}";
        DocumentTitle = doc.DocumentTitle;
        Subtitle = $"Paper: {(doc.PaperWidthMm > 100 ? "A4 Corporate Invoice" : $"{doc.PaperWidthMm}mm Thermal")}";

        LoadPageImages();
        _ = LoadPrintersAsync();
    }

    private async Task LoadPrintersAsync()
    {
        if (_receiptPrinterService == null || _shopSettingService == null) return;
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();
            var printers = await _receiptPrinterService.GetInstalledPrintersAsync();

            AvailablePrinters.Clear();
            foreach (var p in printers)
            {
                AvailablePrinters.Add(p);
            }

            if (!string.IsNullOrWhiteSpace(settings.ReceiptPrinterName) && AvailablePrinters.Contains(settings.ReceiptPrinterName))
            {
                SelectedPrinter = settings.ReceiptPrinterName;
            }
            else
            {
                var candidate = AvailablePrinters.FirstOrDefault(p => p.Contains("TM-T88", StringComparison.OrdinalIgnoreCase) || p.Contains("Receipt", StringComparison.OrdinalIgnoreCase))
                                ?? AvailablePrinters.FirstOrDefault();
                SelectedPrinter = candidate;
            }
        }
        catch
        {
            // Ignore printer enumeration quirks
        }
    }

    // Parameterless constructor for DI and smoke tests
    public ReceiptPreviewDialogViewModel()
    {
        _pdfReportService = null!;
        _receiptPrinterService = null!;
        _shopSettingService = null!;
    }

    private void LoadPageImages()
    {
        if (Document == null || _pdfReportService == null) return;

        try
        {
            var images = _pdfReportService.RenderDocumentPageImages(Document);
            PageImages.Clear();

            foreach (var imgBytes in images)
            {
                using var stream = new MemoryStream(imgBytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                PageImages.Add(bitmap);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not render preview images: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task PrintAsync()
    {
        StatusMessage = null;
        if (Document == null || _receiptPrinterService == null || _shopSettingService == null) return;

        IsLoading = true;
        try
        {
            var settings = await _shopSettingService.GetSettingsAsync();

            if (!string.IsNullOrWhiteSpace(SelectedPrinter))
            {
                settings.ReceiptPrinterName = SelectedPrinter;
            }

            // When user explicitly clicks Print from the preview window, route to WindowsDriver if in PdfPreview mode
            if (settings.PrinterMode == Core.Enums.PrinterMode.PdfPreview)
            {
                settings.PrinterMode = Core.Enums.PrinterMode.WindowsDriver;
            }

            var result = await _receiptPrinterService.PrintReceiptAsync(Document, settings);

            if (result.Success)
            {
                StatusMessage = result.NoticeMessage ?? "Print job sent successfully.";
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Printing failed.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Print error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void SavePdf()
    {
        StatusMessage = null;
        if (PdfBytes == null || PdfBytes.Length == 0)
        {
            StatusMessage = "No PDF data to save.";
            return;
        }

        try
        {
            var sfd = new SaveFileDialog
            {
                Title = "Save Document as PDF",
                FileName = $"{Document?.InvoiceNumber ?? "Document"}.pdf",
                Filter = "PDF Document (*.pdf)|*.pdf|All Files (*.*)|*.*",
                DefaultExt = ".pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                File.WriteAllBytes(sfd.FileName, PdfBytes);
                StatusMessage = $"Saved to '{Path.GetFileName(sfd.FileName)}'.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save PDF: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }
}
