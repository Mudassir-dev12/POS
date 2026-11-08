using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.UI.ViewModels.Reports;

public partial class ReportPreviewDialogViewModel : ObservableObject
{
    private readonly IReceiptPrinterService? _receiptPrinterService;
    private readonly IShopSettingService? _shopSettingService;

    [ObservableProperty]
    private string _title = "Report Preview";

    [ObservableProperty]
    private string _documentTitle = "REPORT PREVIEW";

    [ObservableProperty]
    private string _subtitle = "A4 Document";

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

    public event Action? RequestClose;

    public ReportPreviewDialogViewModel()
    {
        // Parameterless constructor for tests and DI
    }

    public ReportPreviewDialogViewModel(
        string title,
        byte[] pdfBytes,
        IEnumerable<byte[]> pageImageBytes,
        IReceiptPrinterService? receiptPrinterService = null,
        IShopSettingService? shopSettingService = null)
    {
        Title = title;
        DocumentTitle = title.ToUpperInvariant();
        _pdfBytes = pdfBytes;
        _receiptPrinterService = receiptPrinterService;
        _shopSettingService = shopSettingService;

        LoadImages(pageImageBytes);
        _ = LoadPrintersAsync();
    }

    private void LoadImages(IEnumerable<byte[]> imageBytesList)
    {
        PageImages.Clear();
        foreach (var imgBytes in imageBytesList)
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
        Subtitle = $"A4 Document ({PageImages.Count} page{(PageImages.Count == 1 ? "" : "s")})";
    }

    private async Task LoadPrintersAsync()
    {
        if (_receiptPrinterService == null) return;
        try
        {
            var printers = await _receiptPrinterService.GetInstalledPrintersAsync();
            AvailablePrinters.Clear();
            foreach (var p in printers)
            {
                AvailablePrinters.Add(p);
            }

            if (AvailablePrinters.Count > 0)
            {
                SelectedPrinter = AvailablePrinters.FirstOrDefault(p => !p.Contains("Receipt", StringComparison.OrdinalIgnoreCase)) 
                                ?? AvailablePrinters[0];
            }
        }
        catch
        {
            // Ignore printer enumeration failures
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
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"{DocumentTitle.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                Title = "Save Report PDF"
            };

            if (sfd.ShowDialog() == true)
            {
                File.WriteAllBytes(sfd.FileName, PdfBytes);
                StatusMessage = $"Saved: {Path.GetFileName(sfd.FileName)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Print()
    {
        StatusMessage = null;
        if (PageImages.Count == 0)
        {
            StatusMessage = "No pages to print.";
            return;
        }

        try
        {
            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                foreach (var img in PageImages)
                {
                    var visual = new System.Windows.Controls.Image
                    {
                        Source = img,
                        Stretch = System.Windows.Media.Stretch.Uniform
                    };
                    visual.Measure(new System.Windows.Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
                    visual.Arrange(new System.Windows.Rect(new System.Windows.Point(0, 0), visual.DesiredSize));
                    printDialog.PrintVisual(visual, DocumentTitle);
                }
                StatusMessage = "Report sent to printer.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Print error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }
}

