using System.Text;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

/// <summary>
/// Generates ESC/POS thermal printer byte streams for 58mm and 80mm roll printers.
/// </summary>
public static class EscPosGenerator
{
    // ESC/POS Command Constants
    public static readonly byte[] CmdInit = [0x1B, 0x40];                               // ESC @ (Initialize)
    public static readonly byte[] CmdCodePageCp437 = [0x1B, 0x74, 0x00];                 // ESC t 0 (CP437)
    public static readonly byte[] CmdAlignLeft = [0x1B, 0x61, 0x00];                     // ESC a 0 (Left align)
    public static readonly byte[] CmdAlignCenter = [0x1B, 0x61, 0x01];                   // ESC a 1 (Center align)
    public static readonly byte[] CmdAlignRight = [0x1B, 0x61, 0x02];                    // ESC a 2 (Right align)
    public static readonly byte[] CmdBoldOn = [0x1B, 0x45, 0x01];                        // ESC E 1 (Bold ON)
    public static readonly byte[] CmdBoldOff = [0x1B, 0x45, 0x00];                       // ESC E 0 (Bold OFF)
    public static readonly byte[] CmdDoubleSizeOn = [0x1D, 0x21, 0x11];                   // GS ! 0x11 (Double height & width)
    public static readonly byte[] CmdDoubleHeightOn = [0x1D, 0x21, 0x01];                 // GS ! 0x01 (Double height)
    public static readonly byte[] CmdNormalSize = [0x1D, 0x21, 0x00];                     // GS ! 0x00 (Normal size)
    public static readonly byte[] CmdLineFeed = [0x0A];                                   // LF (Line feed)
    public static readonly byte[] CmdPaperCut = [0x1D, 0x56, 0x41, 0x03];                 // GS V 65 3 (Feed 3 lines and cut)
    public static readonly byte[] CmdDrawerPulsePin2 = [0x1B, 0x70, 0x00, 0x19, 0xFA];   // ESC p 0 25 250 (Cash drawer pulse Pin 2)
    public static readonly byte[] CmdDrawerPulsePin5 = [0x1B, 0x70, 0x01, 0x19, 0xFA];   // ESC p 1 25 250 (Cash drawer pulse Pin 5)

    public static int GetColumnsForWidth(int widthMm)
    {
        return widthMm <= 58 ? 32 : 48;
    }

    /// <summary>
    /// Generates standard cash drawer kick pulse bytes for both pin 2 and pin 5.
    /// </summary>
    public static byte[] GenerateDrawerPulse()
    {
        var ms = new MemoryStream();
        ms.Write(CmdDrawerPulsePin2, 0, CmdDrawerPulsePin2.Length);
        ms.Write(CmdDrawerPulsePin5, 0, CmdDrawerPulsePin5.Length);
        return ms.ToArray();
    }

    /// <summary>
    /// Generates full ESC/POS byte buffer from a ReceiptDocument.
    /// </summary>
    public static byte[] GenerateReceiptBytes(ReceiptDocument doc, bool kickDrawer = false)
    {
        using var ms = new MemoryStream();
        int cols = GetColumnsForWidth(doc.PaperWidthMm);
        string cur = NormalizeCurrencySymbol(doc.CurrencySymbol);

        // 1. Initialize & Code Page
        ms.Write(CmdInit, 0, CmdInit.Length);
        ms.Write(CmdCodePageCp437, 0, CmdCodePageCp437.Length);

        // 2. Header (Centered, bold shop name)
        ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);
        ms.Write(CmdDoubleHeightOn, 0, CmdDoubleHeightOn.Length);
        ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
        WriteTextLine(ms, doc.ShopName);
        ms.Write(CmdNormalSize, 0, CmdNormalSize.Length);
        ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

        if (!string.IsNullOrWhiteSpace(doc.ShopAddress))
        {
            foreach (var line in WordWrap(doc.ShopAddress, cols))
            {
                WriteTextLine(ms, line);
            }
        }
        if (!string.IsNullOrWhiteSpace(doc.ShopPhone))
        {
            WriteTextLine(ms, $"Tel: {doc.ShopPhone}");
        }
        if (!string.IsNullOrWhiteSpace(doc.ShopTaxNumber))
        {
            WriteTextLine(ms, $"Tax Reg: {doc.ShopTaxNumber}");
        }

        // Title
        ms.Write(CmdLineFeed, 0, CmdLineFeed.Length);
        ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
        WriteTextLine(ms, $"--- {doc.DocumentTitle} ---");
        ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

        // Duplicate banner
        if (doc.IsDuplicateCopy)
        {
            ms.Write(CmdLineFeed, 0, CmdLineFeed.Length);
            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
            WriteTextLine(ms, "*** DUPLICATE COPY ***");
            WriteTextLine(ms, $"Reprint #{Math.Max(1, doc.ReprintCount)} on {DateTime.Now:yyyy-MM-dd HH:mm}");
            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);
        }

        // Separator
        ms.Write(CmdAlignLeft, 0, CmdAlignLeft.Length);
        WriteTextLine(ms, new string('-', cols));

        // Invoice Meta
        WriteTextLine(ms, Justify($"Doc: {doc.ReceiptNumber}", doc.IssuedAt.ToString("yyyy-MM-dd HH:mm"), cols));
        WriteTextLine(ms, $"Cashier: {doc.CashierName}");
        WriteTextLine(ms, new string('-', cols));

        // Lines
        foreach (var item in doc.Lines)
        {
            var nameLines = WordWrap(item.ProductName, cols);
            foreach (var nl in nameLines)
            {
                WriteTextLine(ms, nl);
            }

            string qtyPrice = $"  {item.Quantity:0.##} x {cur}{item.UnitPrice:F2}";
            string lineTotal = $"{cur}{item.LineTotal:F2}";

            if (item.DiscountAmount > 0m)
            {
                qtyPrice += $" (-{cur}{item.DiscountAmount:F2})";
            }

            WriteTextLine(ms, Justify(qtyPrice, lineTotal, cols));
        }

        WriteTextLine(ms, new string('-', cols));

        // Totals
        WriteTextLine(ms, Justify("Subtotal:", $"{cur}{doc.SubTotal:F2}", cols));
        decimal totalDisc = doc.LineDiscountTotal + doc.BillDiscountAmount;
        if (totalDisc > 0m)
        {
            WriteTextLine(ms, Justify("Discount:", $"-{cur}{totalDisc:F2}", cols));
        }
        if (doc.TaxAmount > 0m)
        {
            WriteTextLine(ms, Justify("Tax / VAT:", $"{cur}{doc.TaxAmount:F2}", cols));
        }
        if (doc.RoundOff != 0m)
        {
            WriteTextLine(ms, Justify("Round-off:", $"{cur}{doc.RoundOff:F2}", cols));
        }

        WriteTextLine(ms, new string('=', cols));

        // Grand Total (Bold)
        ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
        WriteTextLine(ms, Justify("GRAND TOTAL:", $"{cur}{doc.GrandTotal:F2}", cols));
        ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

        WriteTextLine(ms, new string('-', cols));

        // Payments
        foreach (var p in doc.Payments)
        {
            string payDesc = $"Paid ({p.MethodName}):";
            if (!string.IsNullOrWhiteSpace(p.Reference))
            {
                payDesc += $" #{p.Reference}";
            }
            WriteTextLine(ms, Justify(payDesc, $"{cur}{p.Amount:F2}", cols));
        }

        if (doc.ChangeAmount > 0m)
        {
            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
            WriteTextLine(ms, Justify("Change Due:", $"{cur}{doc.ChangeAmount:F2}", cols));
            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);
        }

        if (doc.DueAmount > 0m)
        {
            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
            WriteTextLine(ms, Justify("Balance Due (old):", $"{cur}{doc.DueAmount:F2}", cols));
            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);
        }

        // Footer Text
        WriteTextLine(ms, new string('-', cols));
        ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);
        if (!string.IsNullOrWhiteSpace(doc.FooterMessage))
        {
            foreach (var line in WordWrap(doc.FooterMessage, cols))
            {
                WriteTextLine(ms, line);
            }
        }

        // Barcode
        if (!string.IsNullOrWhiteSpace(doc.BarcodeOrQrValue))
        {
            ms.Write(CmdLineFeed, 0, CmdLineFeed.Length);
            GenerateCode128Barcode(ms, doc.BarcodeOrQrValue);
            ms.Write(CmdLineFeed, 0, CmdLineFeed.Length);
            WriteTextLine(ms, doc.BarcodeOrQrValue);
        }

        // Paper Feed & Cut
        ms.Write(CmdLineFeed, 0, CmdLineFeed.Length);
        ms.Write(CmdLineFeed, 0, CmdLineFeed.Length);
        ms.Write(CmdPaperCut, 0, CmdPaperCut.Length);

        // Optional drawer pulse appended at the very end
        if (kickDrawer)
        {
            ms.Write(CmdDrawerPulsePin2, 0, CmdDrawerPulsePin2.Length);
            ms.Write(CmdDrawerPulsePin5, 0, CmdDrawerPulsePin5.Length);
        }

        return ms.ToArray();
    }

    public static void GenerateCode128Barcode(MemoryStream ms, string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        // GS h 60 (Height = 60 dots)
        ms.Write([0x1D, 0x68, 60], 0, 3);
        // GS w 2 (Module width = 2)
        ms.Write([0x1D, 0x77, 2], 0, 3);
        // GS H 0 (Turn off HRI characters below barcode; we print our own text below)
        ms.Write([0x1D, 0x48, 0], 0, 3);

        // GS k 73 len {B} data
        // Code 128 subset B header: {B} is 0x7B, 0x42
        var rawData = Encoding.ASCII.GetBytes(data);
        var barcodePayload = new byte[2 + rawData.Length];
        barcodePayload[0] = 0x7B; // '{'
        barcodePayload[1] = 0x42; // 'B'
        Buffer.BlockCopy(rawData, 0, barcodePayload, 2, rawData.Length);

        ms.Write([0x1D, 0x6B, 73, (byte)barcodePayload.Length], 0, 4);
        ms.Write(barcodePayload, 0, barcodePayload.Length);
    }

    public static string NormalizeCurrencySymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return "Rs";
        return symbol switch
        {
            "Rs" => "Rs",
            "$" => "$",
            "£" => "\x9C", // CP437 pound
            "¥" => "\x9D", // CP437 yen
            "€" => "EUR ",
            "₹" => "Rs.",
            _ => symbol.Length > 3 ? symbol[..3] : symbol
        };
    }

    public static string Justify(string left, string right, int width)
    {
        left ??= string.Empty;
        right ??= string.Empty;

        int space = width - left.Length - right.Length;
        if (space <= 0)
        {
            return left + " " + right;
        }

        return left + new string(' ', space) + right;
    }

    public static List<string> WordWrap(string text, int maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            if (currentLine.Length == 0)
            {
                if (word.Length > maxWidth)
                {
                    // Split long unbroken word
                    for (int i = 0; i < word.Length; i += maxWidth)
                    {
                        lines.Add(word.Substring(i, Math.Min(maxWidth, word.Length - i)));
                    }
                }
                else
                {
                    currentLine.Append(word);
                }
            }
            else if (currentLine.Length + 1 + word.Length <= maxWidth)
            {
                currentLine.Append(' ').Append(word);
            }
            else
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
                if (word.Length > maxWidth)
                {
                    for (int i = 0; i < word.Length; i += maxWidth)
                    {
                        lines.Add(word.Substring(i, Math.Min(maxWidth, word.Length - i)));
                    }
                }
                else
                {
                    currentLine.Append(word);
                }
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString());
        }

        return lines;
    }

    private static void WriteTextLine(MemoryStream ms, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text + "\n");
        ms.Write(bytes, 0, bytes.Length);
    }
}
