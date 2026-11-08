using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ShopPOS.Data.Services;

/// <summary>
/// Provides low-level Win32 spooler communication and Windows driver printing for thermal POS printers.
/// </summary>
[SupportedOSPlatform("windows")]
public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string pDocName = "ShopPOS Receipt";

        [MarshalAs(UnmanagedType.LPStr)]
        public string? pOutputFile = null;

        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDataType = "RAW";
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    /// <summary>
    /// Safely enumerates installed Windows printer queues without throwing unhandled exceptions if the spooler service is stopped.
    /// </summary>
    public static IReadOnlyList<string> GetInstalledPrinters()
    {
        var list = new List<string>();
        try
        {
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                if (!string.IsNullOrWhiteSpace(printer))
                {
                    list.Add(printer);
                }
            }
        }
        catch
        {
            // Print spooler service may be stopped or disabled
        }
        return list;
    }

    /// <summary>
    /// Sends raw byte buffer (e.g. ESC/POS commands) directly to the named Windows printer spooler queue.
    /// </summary>
    public static bool SendBytesToPrinter(string printerName, byte[] bytes, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(printerName))
        {
            errorMessage = "No printer name configured.";
            return false;
        }

        if (bytes == null || bytes.Length == 0)
        {
            errorMessage = "No print data to send.";
            return false;
        }

        IntPtr hPrinter = IntPtr.Zero;
        IntPtr pUnmanagedBytes = IntPtr.Zero;

        try
        {
            if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"Could not open printer '{printerName}'. (Win32 Error: {err}). Please ensure the printer is turned on, connected, and the Windows Print Spooler service is running.";
                return false;
            }

            var di = new DOCINFOA();
            if (!StartDocPrinter(hPrinter, 1, di))
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"Failed to start print job on '{printerName}'. (Win32 Error: {err}).";
                return false;
            }

            if (!StartPagePrinter(hPrinter))
            {
                int err = Marshal.GetLastWin32Error();
                EndDocPrinter(hPrinter);
                errorMessage = $"Failed to start page on '{printerName}'. (Win32 Error: {err}).";
                return false;
            }

            pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);

            if (!WritePrinter(hPrinter, pUnmanagedBytes, bytes.Length, out int bytesWritten) || bytesWritten != bytes.Length)
            {
                int err = Marshal.GetLastWin32Error();
                EndPagePrinter(hPrinter);
                EndDocPrinter(hPrinter);
                errorMessage = $"Failed to write raw data to printer '{printerName}'. (Win32 Error: {err}).";
                return false;
            }

            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Unexpected error communicating with printer '{printerName}': {ex.Message}";
            return false;
        }
        finally
        {
            if (pUnmanagedBytes != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pUnmanagedBytes);
            }
            if (hPrinter != IntPtr.Zero)
            {
                ClosePrinter(hPrinter);
            }
        }
    }

    /// <summary>
    /// Configures paper size matching driver-defined roll paper sizes or standard fallback.
    /// </summary>
    private static void ConfigurePaperSize(PrintDocument printDoc, int paperWidthMm)
    {
        int widthHundredths = paperWidthMm <= 58 ? 228 : 315;
        PaperSize? matched = null;

        try
        {
            foreach (PaperSize ps in printDoc.PrinterSettings.PaperSizes)
            {
                if (paperWidthMm <= 58)
                {
                    if (ps.PaperName.Contains("58", StringComparison.OrdinalIgnoreCase) || Math.Abs(ps.Width - 228) <= 20)
                    {
                        matched = ps;
                        break;
                    }
                }
                else
                {
                    if (ps.PaperName.Contains("80", StringComparison.OrdinalIgnoreCase) || Math.Abs(ps.Width - 315) <= 20)
                    {
                        matched = ps;
                        break;
                    }
                }
            }
        }
        catch
        {
            // Ignore driver enumeration quirks
        }

        if (matched != null)
        {
            printDoc.DefaultPageSettings.PaperSize = matched;
        }
        else
        {
            printDoc.DefaultPageSettings.PaperSize = new PaperSize("ThermalRoll", widthHundredths, 1169);
        }

        printDoc.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);
    }

    /// <summary>
    /// Prints rendered page images (PNG/JPEG) through standard Windows GDI print driver.
    /// Produces pixel-perfect thermal receipts identical to browser/PDF printing.
    /// </summary>
    public static bool PrintImagesViaWindowsDriver(string printerName, IReadOnlyList<byte[]> pageImages, int paperWidthMm, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(printerName))
        {
            errorMessage = "No printer name configured.";
            return false;
        }

        if (pageImages == null || pageImages.Count == 0)
        {
            errorMessage = "No page images to print.";
            return false;
        }

        try
        {
            using var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = printerName;

            if (!printDoc.PrinterSettings.IsValid)
            {
                errorMessage = $"Printer '{printerName}' is not valid or not recognized by Windows.";
                return false;
            }

            ConfigurePaperSize(printDoc, paperWidthMm);

            int pageIndex = 0;
            printDoc.PrintPage += (sender, e) =>
            {
                if (pageIndex < pageImages.Count)
                {
                    using var ms = new MemoryStream(pageImages[pageIndex]);
                    using var img = Image.FromStream(ms);

                    float printableWidth = e.MarginBounds.Width > 0 ? e.MarginBounds.Width : (paperWidthMm <= 58 ? 228 : 315);
                    float scale = printableWidth / img.Width;
                    float drawHeight = img.Height * scale;

                    e.Graphics!.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    e.Graphics.DrawImage(img, e.MarginBounds.Left, e.MarginBounds.Top, printableWidth, drawHeight);

                    pageIndex++;
                    e.HasMorePages = pageIndex < pageImages.Count;
                }
                else
                {
                    e.HasMorePages = false;
                }
            };

            printDoc.Print();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Windows driver image print failed for '{printerName}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Prints plain text receipt through standard Windows GDI print driver.
    /// </summary>
    public static bool PrintViaWindowsDriver(string printerName, string text, int paperWidthMm, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(printerName))
        {
            errorMessage = "No printer name configured.";
            return false;
        }

        try
        {
            using var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = printerName;

            if (!printDoc.PrinterSettings.IsValid)
            {
                errorMessage = $"Printer '{printerName}' is not valid or not recognized by Windows.";
                return false;
            }

            ConfigurePaperSize(printDoc, paperWidthMm);

            var stringLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int currentLine = 0;

            printDoc.PrintPage += (sender, e) =>
            {
                using var font = new Font("Consolas", paperWidthMm <= 58 ? 7.5f : 8.5f, FontStyle.Regular);
                using var brush = new SolidBrush(Color.Black);

                float lineHeight = font.GetHeight(e.Graphics!);
                float yPos = e.MarginBounds.Top;
                float xPos = e.MarginBounds.Left;

                while (currentLine < stringLines.Length)
                {
                    string line = stringLines[currentLine];
                    e.Graphics!.DrawString(line, font, brush, xPos, yPos);
                    yPos += lineHeight;
                    currentLine++;

                    if (yPos + lineHeight > e.MarginBounds.Bottom)
                    {
                        e.HasMorePages = true;
                        return;
                    }
                }

                e.HasMorePages = false;
            };

            printDoc.Print();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Windows driver print failed for '{printerName}': {ex.Message}";
            return false;
        }
    }
}
