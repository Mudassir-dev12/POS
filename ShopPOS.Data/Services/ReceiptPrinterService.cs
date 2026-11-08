using System.Runtime.Versioning;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;

namespace ShopPOS.Data.Services;

[SupportedOSPlatform("windows")]
public class ReceiptPrinterService : IReceiptPrinterService
{
    private readonly IPdfReportService _pdfReportService;
    private readonly IReceiptBuilder _receiptBuilder;
    private readonly IShopSettingService _shopSettingService;

    public ReceiptPrinterService(
        IPdfReportService pdfReportService,
        IReceiptBuilder receiptBuilder,
        IShopSettingService shopSettingService)
    {
        _pdfReportService = pdfReportService;
        _receiptBuilder = receiptBuilder;
        _shopSettingService = shopSettingService;
    }

    public async Task<ReceiptPrintResult> PrintReceiptAsync(ReceiptDocument document, ShopSetting settings, CancellationToken cancellationToken = default)
    {
        int copies = Math.Max(1, settings.ReceiptCopies);
        bool kickDrawer = settings.OpenCashDrawerOnCashSale && document.Payments.Any(p => p.MethodName.Equals("Cash", StringComparison.OrdinalIgnoreCase));

        switch (settings.PrinterMode)
        {
            case PrinterMode.PdfPreview:
            {
                try
                {
                    byte[] pdfBytes = document.PaperWidthMm > 100
                        ? await _pdfReportService.GenerateA4InvoicePdfAsync(document, cancellationToken)
                        : await _pdfReportService.GenerateReceiptPdfAsync(document, cancellationToken);

                    return ReceiptPrintResult.Ok(pdfBytes);
                }
                catch (Exception ex)
                {
                    return ReceiptPrintResult.Fail($"Failed to generate receipt PDF: {ex.Message}");
                }
            }

            case PrinterMode.WindowsDriver:
            {
                string? printerName = settings.ReceiptPrinterName;
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    var installed = RawPrinterHelper.GetInstalledPrinters();
                    printerName = installed.FirstOrDefault(p => p.Contains("TM-T88", StringComparison.OrdinalIgnoreCase) || p.Contains("Receipt", StringComparison.OrdinalIgnoreCase))
                                  ?? installed.FirstOrDefault() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(printerName))
                {
                    return ReceiptPrintResult.Fail("No Windows printer found. Please ensure a printer is installed.");
                }

                // Render page images for pixel-perfect thermal graphics printing matching preview
                var pageImages = _pdfReportService.RenderDocumentPageImages(document);
                bool useImages = pageImages != null && pageImages.Count > 0;
                string receiptText = string.Empty;

                for (int i = 0; i < copies; i++)
                {
                    bool printed = false;
                    string? error = null;

                    if (useImages)
                    {
                        printed = RawPrinterHelper.PrintImagesViaWindowsDriver(
                            printerName,
                            pageImages!,
                            document.PaperWidthMm,
                            out error);
                    }

                    if (!printed)
                    {
                        if (string.IsNullOrEmpty(receiptText))
                        {
                            receiptText = FormatDocumentAsPlainText(document);
                        }

                        printed = RawPrinterHelper.PrintViaWindowsDriver(
                            printerName,
                            receiptText,
                            document.PaperWidthMm,
                            out error);
                    }

                    if (!printed)
                    {
                        return ReceiptPrintResult.Fail(error ?? $"Failed printing to '{printerName}'.");
                    }
                }

                if (kickDrawer)
                {
                    _ = OpenCashDrawerAsync(printerName);
                }

                return ReceiptPrintResult.Ok(notice: $"Sent {copies} copy(ies) to '{printerName}'.");
            }

            case PrinterMode.EscPosRaw:
            {
                string? rawPrinterName = settings.ReceiptPrinterName;
                if (string.IsNullOrWhiteSpace(rawPrinterName))
                {
                    var installed = RawPrinterHelper.GetInstalledPrinters();
                    rawPrinterName = installed.FirstOrDefault(p => p.Contains("TM-T88", StringComparison.OrdinalIgnoreCase) || p.Contains("Receipt", StringComparison.OrdinalIgnoreCase))
                                     ?? installed.FirstOrDefault() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(rawPrinterName))
                {
                    return ReceiptPrintResult.Fail("No receipt printer selected. Please configure a thermal printer in Settings.");
                }

                byte[] rawBytes = EscPosGenerator.GenerateReceiptBytes(document, kickDrawer);

                for (int i = 0; i < copies; i++)
                {
                    bool sent = RawPrinterHelper.SendBytesToPrinter(
                        rawPrinterName,
                        rawBytes,
                        out string? error);

                    if (!sent)
                    {
                        return ReceiptPrintResult.Fail(error ?? $"Failed sending raw ESC/POS bytes to '{rawPrinterName}'.");
                    }
                }

                return ReceiptPrintResult.Ok(notice: $"Sent {copies} copy(ies) to '{rawPrinterName}'.");
            }

            default:
                return ReceiptPrintResult.Fail($"Unsupported printer mode: {settings.PrinterMode}");
        }
    }

    public async Task<ReceiptPrintResult> PrintSaleReceiptAsync(SaleDetailDto sale, ShopSetting settings, bool isDuplicate = false, int reprintCount = 0, CancellationToken cancellationToken = default)
    {
        var doc = _receiptBuilder.BuildFromSale(sale, settings, isDuplicate, reprintCount);
        return await PrintReceiptAsync(doc, settings, cancellationToken);
    }

    public async Task<ReceiptPrintResult> PrintReturnReceiptAsync(SaleReturnDetailDto returnDetail, ShopSetting settings, bool isDuplicate = false, CancellationToken cancellationToken = default)
    {
        var doc = _receiptBuilder.BuildFromSaleReturn(returnDetail, settings, isDuplicate);
        return await PrintReceiptAsync(doc, settings, cancellationToken);
    }

    public async Task<ReceiptPrintResult> PrintTestReceiptAsync(ShopSetting settings, CancellationToken cancellationToken = default)
    {
        var doc = _receiptBuilder.BuildTestReceipt(settings);
        return await PrintReceiptAsync(doc, settings, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetInstalledPrintersAsync()
    {
        return Task.FromResult(RawPrinterHelper.GetInstalledPrinters());
    }

    public Task<bool> CheckPrinterStatusAsync(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName)) return Task.FromResult(false);
        var printers = RawPrinterHelper.GetInstalledPrinters();
        return Task.FromResult(printers.Contains(printerName, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<ReceiptPrintResult> OpenCashDrawerAsync(string? printerName = null)
    {
        string? targetPrinter = printerName;
        if (string.IsNullOrWhiteSpace(targetPrinter))
        {
            try
            {
                var settings = await _shopSettingService.GetSettingsAsync();
                targetPrinter = settings.ReceiptPrinterName;
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(targetPrinter))
        {
            return ReceiptPrintResult.Fail("No printer configured to send cash drawer pulse.");
        }

        byte[] drawerBytes = EscPosGenerator.GenerateDrawerPulse();
        bool sent = RawPrinterHelper.SendBytesToPrinter(targetPrinter, drawerBytes, out string? error);

        return sent
            ? ReceiptPrintResult.Ok(notice: $"Cash drawer pulse sent to '{targetPrinter}'.")
            : ReceiptPrintResult.Fail(error ?? "Failed to send cash drawer kick pulse.");
    }

    private static string FormatDocumentAsPlainText(ReceiptDocument doc)
    {
        int cols = EscPosGenerator.GetColumnsForWidth(doc.PaperWidthMm);
        string cur = doc.CurrencySymbol;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(Center(doc.ShopName, cols));
        if (!string.IsNullOrWhiteSpace(doc.ShopAddress)) sb.AppendLine(Center(doc.ShopAddress, cols));
        if (!string.IsNullOrWhiteSpace(doc.ShopPhone)) sb.AppendLine(Center($"Tel: {doc.ShopPhone}", cols));
        if (!string.IsNullOrWhiteSpace(doc.ShopTaxNumber)) sb.AppendLine(Center($"Tax Reg: {doc.ShopTaxNumber}", cols));

        sb.AppendLine(Center($"--- {doc.DocumentTitle} ---", cols));
        if (doc.IsDuplicateCopy)
        {
            sb.AppendLine(Center("*** DUPLICATE COPY ***", cols));
        }

        sb.AppendLine(new string('-', cols));
        sb.AppendLine(EscPosGenerator.Justify($"Doc: {doc.ReceiptNumber}", doc.IssuedAt.ToString("yyyy-MM-dd HH:mm"), cols));
        sb.AppendLine($"Cashier: {doc.CashierName}");
        sb.AppendLine(new string('-', cols));

        foreach (var item in doc.Lines)
        {
            foreach (var nl in EscPosGenerator.WordWrap(item.ProductName, cols))
            {
                sb.AppendLine(nl);
            }
            string sub = $"  {item.Quantity:0.##} x {cur} {item.UnitPrice:F2}";
            if (item.DiscountAmount > 0) sub += $" (-{cur} {item.DiscountAmount:F2})";
            sb.AppendLine(EscPosGenerator.Justify(sub, $"{cur} {item.LineTotal:F2}", cols));
        }

        sb.AppendLine(new string('-', cols));
        sb.AppendLine(EscPosGenerator.Justify("Subtotal:", $"{cur} {doc.SubTotal:F2}", cols));
        decimal disc = doc.LineDiscountTotal + doc.BillDiscountAmount;
        if (disc > 0) sb.AppendLine(EscPosGenerator.Justify("Discount:", $"-{cur} {disc:F2}", cols));
        if (doc.TaxAmount > 0) sb.AppendLine(EscPosGenerator.Justify("Tax:", $"{cur} {doc.TaxAmount:F2}", cols));
        if (doc.RoundOff != 0) sb.AppendLine(EscPosGenerator.Justify("Round-off:", $"{cur} {doc.RoundOff:F2}", cols));
        sb.AppendLine(new string('=', cols));
        sb.AppendLine(EscPosGenerator.Justify("GRAND TOTAL:", $"{cur} {doc.GrandTotal:F2}", cols));
        sb.AppendLine(new string('-', cols));

        foreach (var p in doc.Payments)
        {
            sb.AppendLine(EscPosGenerator.Justify($"Paid ({p.MethodName}):", $"{cur} {p.Amount:F2}", cols));
        }
        if (doc.ChangeAmount > 0) sb.AppendLine(EscPosGenerator.Justify("Change Due:", $"{cur} {doc.ChangeAmount:F2}", cols));
        if (doc.DueAmount > 0) sb.AppendLine(EscPosGenerator.Justify("Balance Due:", $"{cur} {doc.DueAmount:F2}", cols));

        sb.AppendLine(new string('-', cols));
        if (!string.IsNullOrWhiteSpace(doc.FooterMessage)) sb.AppendLine(Center(doc.FooterMessage, cols));

        return sb.ToString();
    }

    private static string Center(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length >= width) return text;
        int pad = (width - text.Length) / 2;
        return new string(' ', pad) + text;
    }
}
