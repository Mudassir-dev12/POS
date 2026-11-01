using System.Text;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

public class SalesExportService : ISalesExportService
{
    public Task<byte[]> ExportSalesToCsvAsync(IReadOnlyList<SaleSummaryDto> sales, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        // UTF-8 BOM is added by UTF8Encoding(true)
        sb.AppendLine("Invoice #,Date & Time,Customer,Items,Total,Paid,Due,Payment Methods,Status");

        foreach (var sale in sales)
        {
            string dateStr = sale.SaleDate.ToString("yyyy-MM-dd HH:mm");
            string customerStr = EscapeCsv(sale.CustomerName ?? "Walk-in");
            string methodsStr = EscapeCsv(sale.PaymentMethodsSummary);
            string statusStr = sale.Status.ToString();

            sb.AppendLine($"{EscapeCsv(sale.SaleNumber)},{dateStr},{customerStr},{sale.ItemCount},{sale.GrandTotal:F2},{sale.PaidAmount:F2},{sale.DueAmount:F2},{methodsStr},{statusStr}");
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        byte[] bytes = encoding.GetBytes(sb.ToString());
        return Task.FromResult(bytes);
    }

    private static string EscapeCsv(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "\"\"";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}

