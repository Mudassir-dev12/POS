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
        WriteTextLine(ms, Justify($"Doc: {doc.InvoiceNumber}", doc.DateTime.ToString("yyyy-MM-dd HH:mm"), cols));
        WriteTextLine(ms, $"Cashier: {doc.CashierName}");
        if (!string.IsNullOrWhiteSpace(doc.CustomerName))
        {
            WriteTextLine(ms, $"Customer: {doc.CustomerName}");
            if (!string.IsNullOrWhiteSpace(doc.CustomerPhone))
            {
                WriteTextLine(ms, $"Cust Phone: {doc.CustomerPhone}");
            }
            if (!string.IsNullOrWhiteSpace(doc.CustomerTaxNumber))
            {
                WriteTextLine(ms, $"Cust Tax #: {doc.CustomerTaxNumber}");
            }
        }

        WriteTextLine(ms, new string('-', cols));

        // 3. Shift Report Data (if shift document)
        if (doc.ShiftData != null)
        {
            var s = doc.ShiftData;
            WriteTextLine(ms, Justify("Shift Number:", s.ShiftNumber, cols));
            WriteTextLine(ms, Justify("Shift Status:", s.Status.ToString(), cols));
            WriteTextLine(ms, Justify("Opened At:", s.OpenedAt.ToString("yyyy-MM-dd HH:mm"), cols));
            if (s.ClosedAt.HasValue)
            {
                WriteTextLine(ms, Justify("Closed At:", s.ClosedAt.Value.ToString("yyyy-MM-dd HH:mm"), cols));
            }
            WriteTextLine(ms, new string('-', cols));

            WriteTextLine(ms, Justify("Opening Cash:", $"{cur}{s.OpeningCash:F2}", cols));
            WriteTextLine(ms, Justify("Gross Sales:", $"{cur}{s.GrossSales:F2}", cols));
            WriteTextLine(ms, Justify("Net Sales:", $"{cur}{s.NetSales:F2}", cols));
            WriteTextLine(ms, Justify("Transactions:", $"{s.SalesCount}", cols));
            WriteTextLine(ms, Justify("Returns Count:", $"{s.ReturnsCount}", cols));
            WriteTextLine(ms, Justify("Total Returns:", $"-{cur}{s.TotalReturns:F2}", cols));
            WriteTextLine(ms, Justify("Cash In (Float):", $"+{cur}{s.CashIn:F2}", cols));
            WriteTextLine(ms, Justify("Cash Out (Expense):", $"-{cur}{s.CashOut:F2}", cols));

            WriteTextLine(ms, new string('-', cols));
            WriteTextLine(ms, "PAYMENTS BREAKDOWN:");
            foreach (var kvp in s.PaymentsByMethod)
            {
                WriteTextLine(ms, Justify($"  {kvp.Key}:", $"{cur}{kvp.Value:F2}", cols));
            }

            WriteTextLine(ms, new string('=', cols));
            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
            WriteTextLine(ms, Justify("EXPECTED DRAWER CASH:", $"{cur}{s.ExpectedCash:F2}", cols));
            if (s.CountedCash.HasValue)
            {
                WriteTextLine(ms, Justify("ACTUAL COUNTED CASH:", $"{cur}{s.CountedCash.Value:F2}", cols));
                string diffLabel = (s.Difference ?? 0m) >= 0 ? "OVERAGE:" : "SHORTAGE:";
                WriteTextLine(ms, Justify(diffLabel, $"{cur}{s.Difference ?? 0m:F2}", cols));
            }
            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);
        }
        else
        {
            // 4. Sales / Returns Line Items
            // Columns layout:
            // Line 1: Item Name (word-wrapped)
            // Line 2: "  Qty x UnitPrice" [Disc] [LineTotal]
            foreach (var item in doc.Lines)
            {
                var nameLines = WordWrap(item.ItemName, cols);
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

            // 5. Totals
            WriteTextLine(ms, Justify("Subtotal:", $"{cur}{doc.SubTotal:F2}", cols));
            if (doc.TotalDiscount > 0m)
            {
                WriteTextLine(ms, Justify("Discount:", $"-{cur}{doc.TotalDiscount:F2}", cols));
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
                string payDesc = $"Paid ({p.Method}):";
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
                WriteTextLine(ms, Justify("Balance Due:", $"{cur}{doc.DueAmount:F2}", cols));
                ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

                if (doc.CustomerPreviousBalance.HasValue)
                {
                    WriteTextLine(ms, Justify("Prev Balance:", $"{cur}{doc.CustomerPreviousBalance.Value:F2}", cols));
                    WriteTextLine(ms, Justify("Total Account Balance:", $"{cur}{(doc.CustomerNewBalance ?? 0m):F2}", cols));
                }
            }
        }

        // 6. Footer Text
        WriteTextLine(ms, new string('-', cols));
        ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);
        if (!string.IsNullOrWhiteSpace(doc.FooterText))
        {
            foreach (var line in WordWrap(doc.FooterText, cols))
            {
                WriteTextLine(ms, line);
            }
        }

        // 7. Barcode (Code 128) if invoice number provided
        if (!string.IsNullOrWhiteSpace(doc.BarcodeOrQrValue) && doc.BarcodeOrQrValue.Length >= 3 && doc.BarcodeOrQrValue.Length <= 30)
        {
            ms.Write(CmdLineFeed, 0, CmdLineFeed.Length);
            WriteCode128Barcode(ms, doc.BarcodeOrQrValue);
        }

        // 8. Cash Drawer Kick Pulse (if requested)
        if (kickDrawer)
        {
            var drawerBytes = GenerateDrawerPulse();
            ms.Write(drawerBytes, 0, drawerBytes.Length);
        }

        // 9. Extra feed lines & Cut
        ms.Write(CmdLineFeed, 0, CmdLineFeed.Length);
        ms.Write(CmdLineFeed, 0, CmdLineFeed.Length);
        ms.Write(CmdPaperCut, 0, CmdPaperCut.Length);

        return ms.ToArray();
    }

    private static void WriteTextLine(Stream stream, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text + "\n");
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteCode128Barcode(Stream stream, string code)
    {
        try
        {
            // GS h 64 (height 64 dots)
            stream.Write([0x1D, 0x68, 0x40], 0, 3);
            // GS w 2 (width factor 2)
            stream.Write([0x1D, 0x77, 0x02], 0, 3);
            // GS H 2 (HRI characters printed below)
            stream.Write([0x1D, 0x48, 0x02], 0, 3);

            // GS k 73 (Code 128)
            byte[] codeBytes = Encoding.ASCII.GetBytes(code);
            byte len = (byte)(codeBytes.Length + 2); // {B + data
            stream.Write([0x1D, 0x6B, 0x49, len, 0x7B, 0x42], 0, 6);
            stream.Write(codeBytes, 0, codeBytes.Length);
            stream.Write(CmdLineFeed, 0, CmdLineFeed.Length);
        }
        catch { }
    }

    public static string NormalizeCurrencySymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return "$";
        symbol = symbol.Trim();

        // Standard ASCII or simple symbols
        if (symbol == "$" || symbol == "£" || symbol == "€" || symbol == "¥") return symbol;

        // Clean fallbacks for complex or wide multi-byte currencies
        if (symbol.Contains("₹") || symbol.Equals("INR", StringComparison.OrdinalIgnoreCase)) return "Rs.";
        if (symbol.Contains("PKR") || symbol.Equals("Rs", StringComparison.OrdinalIgnoreCase)) return "Rs.";
        if (symbol.Contains("EUR", StringComparison.OrdinalIgnoreCase)) return "EUR ";
        if (symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase)) return "GBP ";
        if (symbol.Contains("USD", StringComparison.OrdinalIgnoreCase)) return "$";

        return symbol.Length <= 4 ? symbol : symbol[..3] + " ";
    }

    public static string Justify(string left, string right, int width)
    {
        if (left == null) left = string.Empty;
        if (right == null) right = string.Empty;

        int totalLen = left.Length + right.Length;
        if (totalLen >= width)
        {
            // Truncate left if necessary to fit right
            int availForLeft = Math.Max(0, width - right.Length - 1);
            if (left.Length > availForLeft && availForLeft > 0)
            {
                left = left[..availForLeft];
            }
            totalLen = left.Length + right.Length;
        }

        int spaces = Math.Max(1, width - totalLen);
        return left + new string(' ', spaces) + right;
    }

    public static List<string> WordWrap(string text, int maxWidth)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            if (line.Length <= maxWidth)
            {
                result.Add(line);
                continue;
            }

            var words = line.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                if (currentLine.Length == 0)
                {
                    if (word.Length > maxWidth)
                    {
                        // Split overly long word
                        int pos = 0;
                        while (pos < word.Length)
                        {
                            int take = Math.Min(maxWidth, word.Length - pos);
                            result.Add(word.Substring(pos, take));
                            pos += take;
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
                    result.Add(currentLine.ToString());
                    currentLine.Clear();
                    if (word.Length > maxWidth)
                    {
                        int pos = 0;
                        while (pos < word.Length)
                        {
                            int take = Math.Min(maxWidth, word.Length - pos);
                            result.Add(word.Substring(pos, take));
                            pos += take;
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
                result.Add(currentLine.ToString());
            }
        }

        return result;
    }
}

