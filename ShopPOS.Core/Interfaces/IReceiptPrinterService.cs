using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Service for thermal POS receipt, invoice generation, and cash drawer interaction.
/// </summary>
public interface IReceiptPrinterService
{
    /// <summary>
    /// Core printer dispatcher: prints the specified document using the configured ShopSetting mode.
    /// </summary>
    Task<ReceiptPrintResult> PrintReceiptAsync(ReceiptDocument document, ShopSetting settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds and prints a sales receipt for a completed transaction.
    /// </summary>
    Task<ReceiptPrintResult> PrintSaleReceiptAsync(SaleDetailDto sale, ShopSetting settings, bool isDuplicate = false, int reprintCount = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds and prints a return slip for a completed sale return.
    /// </summary>
    Task<ReceiptPrintResult> PrintReturnReceiptAsync(SaleReturnDetailDto returnDetail, ShopSetting settings, bool isDuplicate = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds and prints a shift reading or closing report (X or Z report).
    /// </summary>
    Task<ReceiptPrintResult> PrintShiftReportAsync(ShiftSummaryDto shift, ShopSetting settings, bool isZReport, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prints a test receipt to verify printer connectivity, alignment, and paper cut.
    /// </summary>
    Task<ReceiptPrintResult> PrintTestReceiptAsync(ShopSetting settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the list of printer queues currently installed in Windows.
    /// </summary>
    Task<IReadOnlyList<string>> GetInstalledPrintersAsync();

    /// <summary>
    /// Checks connectivity to the configured thermal receipt printer.
    /// </summary>
    Task<bool> CheckPrinterStatusAsync(string printerName);

    /// <summary>
    /// Triggers the electronic cash drawer kick pulse via raw printer interface.
    /// </summary>
    Task<ReceiptPrintResult> OpenCashDrawerAsync(string? printerName = null);

    /// <summary>
    /// Legacy compatibility overload.
    /// </summary>
    Task<bool> PrintReceiptAsync(SaleDetailDto sale, ShopSettingDto settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy compatibility overload.
    /// </summary>
    Task<bool> PrintReceiptAsync(int saleId, ShopSetting settings, CancellationToken cancellationToken = default);
}
