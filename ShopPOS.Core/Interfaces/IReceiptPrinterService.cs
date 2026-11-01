using ShopPOS.Core.Entities;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Placeholder interface for thermal POS receipt and kitchen order printing.
/// A full implementation (supporting ESC/POS 58mm and 80mm printers) will be delivered in the POS phase.
/// </summary>
public interface IReceiptPrinterService
{
    /// <summary>
    /// Prints a sales receipt for a completed transaction.
    /// </summary>
    /// <param name="saleId">Identifier of the completed sale.</param>
    /// <param name="settings">Shop setting containing receipt header/footer and width configurations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if print job sent successfully; otherwise, <c>false</c>.</returns>
    Task<bool> PrintReceiptAsync(int saleId, ShopSetting settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks connectivity to the configured thermal receipt printer.
    /// </summary>
    /// <param name="printerName">System printer name or IP/COM port identifier.</param>
    /// <returns><c>true</c> if printer is accessible and ready; otherwise, <c>false</c>.</returns>
    Task<bool> CheckPrinterStatusAsync(string printerName);

    /// <summary>
    /// Triggers the electronic cash drawer kick pulse via the printer interface.
    /// </summary>
    /// <param name="printerName">System printer name.</param>
    /// <returns><c>true</c> if command delivered successfully; otherwise, <c>false</c>.</returns>
    Task<bool> OpenCashDrawerAsync(string printerName);
}

