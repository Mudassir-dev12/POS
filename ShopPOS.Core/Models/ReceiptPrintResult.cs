namespace ShopPOS.Core.Models;

/// <summary>
/// Result returned by receipt printing operations.
/// </summary>
public class ReceiptPrintResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? PdfBytes { get; set; }
    public string? NoticeMessage { get; set; }

    public static ReceiptPrintResult Ok(byte[]? pdfBytes = null, string? notice = null) =>
        new() { Success = true, PdfBytes = pdfBytes, NoticeMessage = notice };

    public static ReceiptPrintResult Fail(string error, byte[]? pdfBytes = null) =>
        new() { Success = false, ErrorMessage = error, PdfBytes = pdfBytes };
}

