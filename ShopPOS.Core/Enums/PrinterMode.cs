namespace ShopPOS.Core.Enums;

/// <summary>
/// Output destination and protocol for receipt printing.
/// </summary>
public enum PrinterMode
{
    /// <summary>
    /// Displays receipt in an interactive PDF preview dialog with print/save options.
    /// Guaranteed to work on all machines with no printer installed.
    /// </summary>
    PdfPreview = 0,

    /// <summary>
    /// Prints receipt using the standard Windows GDI printer driver installed on the PC.
    /// Works with any thermal printer that has a Windows driver.
    /// </summary>
    WindowsDriver = 1,

    /// <summary>
    /// Sends raw ESC/POS byte commands directly to the named Windows printer spooler queue.
    /// Enables hardware paper cut, font sizing, and cash drawer kick pulse.
    /// </summary>
    EscPosRaw = 2
}

