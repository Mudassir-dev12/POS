using System.Globalization;
using System.Text;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

public class SupplierLedgerService : ISupplierLedgerService
{
    private readonly ISupplierLedgerRepository _ledgerRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly IShopSettingRepository _shopSettingRepo;
    private readonly IPdfReportService _pdfReportService;

    public SupplierLedgerService(
        ISupplierLedgerRepository ledgerRepo,
        ISupplierRepository supplierRepo,
        IShopSettingRepository shopSettingRepo,
                IPdfReportService pdfReportService)
    {
        _ledgerRepo = ledgerRepo;
        _supplierRepo = supplierRepo;
        _shopSettingRepo = shopSettingRepo;
        _pdfReportService = pdfReportService;
    }

    public async Task<Result<decimal>> GetSupplierBalanceAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var balance = await _ledgerRepo.GetSupplierBalanceAsync(supplierId, cancellationToken);
        return Result.Ok(balance);
    }

    public async Task<Result<Dictionary<int, decimal>>> GetSupplierBalancesAsync(IEnumerable<int> supplierIds, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var map = await _ledgerRepo.GetSupplierBalancesAsync(supplierIds, cancellationToken);
        return Result.Ok(map);
    }

    public async Task<Result<SupplierLedgerReportDto>> GetSupplierLedgerReportAsync(int supplierId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        var supplier = await _supplierRepo.GetByIdAsync(supplierId, cancellationToken);
        if (supplier == null)
        {
            return Result.Failure<SupplierLedgerReportDto>($"Supplier ID {supplierId} was not found.");
        }

        var report = await _ledgerRepo.GetSupplierLedgerReportAsync(supplierId, fromDate, toDate, cancellationToken);
        return Result.Ok(report);
    }

    public async Task<Result<byte[]>> ExportLedgerCsvAsync(int supplierId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var reportRes = await GetSupplierLedgerReportAsync(supplierId, fromDate, toDate, cancellationToken);
        if (!reportRes.Success || reportRes.Data == null)
        {
            return Result.Failure<byte[]>(reportRes.ErrorMessage ?? "Failed to generate report.");
        }

        var l = reportRes.Data;
        var sb = new StringBuilder();
        // UTF-8 BOM
        sb.AppendLine($"Supplier Ledger: {l.SupplierName}");
        sb.AppendLine($"Period: {l.FromDate:yyyy-MM-dd} to {l.ToDate:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("Date,Type,Reference,Notes,Debit,Credit,Running Balance");
        sb.AppendLine($"{l.FromDate:yyyy-MM-dd},Opening,Balance Forward,,0.00,0.00,{l.OpeningBalance.ToString("F2", CultureInfo.InvariantCulture)}");

        foreach (var entry in l.Entries)
        {
            string debit = entry.Debit > 0 ? entry.Debit.ToString("F2", CultureInfo.InvariantCulture) : string.Empty;
            string credit = entry.Credit > 0 ? entry.Credit.ToString("F2", CultureInfo.InvariantCulture) : string.Empty;
            string note = entry.Notes != null ? $"\"{entry.Notes.Replace("\"", "\"\"")}\"" : string.Empty;
            sb.AppendLine($"{entry.Date:yyyy-MM-dd},{entry.Type},{entry.Reference},{note},{debit},{credit},{entry.RunningBalance.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        sb.AppendLine();
        sb.AppendLine($"TOTALS,,,,{l.TotalDebits.ToString("F2", CultureInfo.InvariantCulture)},{l.TotalCredits.ToString("F2", CultureInfo.InvariantCulture)},{l.ClosingBalance.ToString("F2", CultureInfo.InvariantCulture)}");

        byte[] bytes = new UTF8Encoding(true).GetBytes(sb.ToString());
        return Result.Ok(bytes);
    }

    public async Task<Result<byte[]>> ExportLedgerPdfAsync(int supplierId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var reportRes = await GetSupplierLedgerReportAsync(supplierId, fromDate, toDate, cancellationToken);
        if (!reportRes.Success || reportRes.Data == null)
        {
            return Result.Failure<byte[]>(reportRes.ErrorMessage ?? "Failed to generate report.");
        }

        var settings = await _shopSettingRepo.GetCurrentSettingsAsync(cancellationToken);
        var settingsDto = new ShopSettingDto
        {
            ShopName = settings?.ShopName ?? "Shop POS",
            Address = settings?.Address,
            Phone = settings?.Phone,
            TaxNumber = settings?.TaxNumber,
            CurrencySymbol = settings?.CurrencySymbol ?? "Rs"
        };

        var bytes = await _pdfReportService.GenerateSupplierLedgerPdfAsync(reportRes.Data, settingsDto, cancellationToken);
        return Result.Ok(bytes);
    }
}
