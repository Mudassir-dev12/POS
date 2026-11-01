using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

public class PdfReportService : IPdfReportService
{
    static PdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GeneratePurchaseInvoicePdfAsync(PurchaseDetailDto purchase, ShopSettingDto settings, CancellationToken cancellationToken = default)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                page.Header().Element(c => ComposePurchaseHeader(c, purchase, settings));
                page.Content().Element(c => ComposePurchaseContent(c, purchase, settings));
                page.Footer().Element(c => ComposePurchaseFooter(c, purchase));
            });
        });

        byte[] pdfBytes = doc.GeneratePdf();
        return Task.FromResult(pdfBytes);
    }

    public Task<byte[]> GenerateStockOverviewPdfAsync(IReadOnlyList<StockOverviewItemDto> items, ShopSettingDto settings, bool isCashier, CancellationToken cancellationToken = default)
    {
        var cur = settings.CurrencySymbol;
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                page.Header().Column(col =>
                {
                    col.Item().Text(settings.ShopName).FontSize(16).Bold().FontColor("#1E293B");
                    col.Item().Text("INVENTORY STOCK OVERVIEW REPORT").FontSize(12).SemiBold().FontColor("#475569");
                    col.Item().Text($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8).FontColor("#64748B");
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor("#CBD5E1");
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(70);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.ConstantColumn(45);
                            columns.ConstantColumn(50);
                            columns.ConstantColumn(50);
                            if (!isCashier)
                            {
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(65);
                            }
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#F1F5F9").Padding(4).Text("SKU").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).Text("Product").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).Text("Category").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).Text("Unit").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Stock").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Alert").Bold();
                            if (!isCashier)
                            {
                                header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Cost").Bold();
                                header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Total Value").Bold();
                            }
                        });

                        int idx = 0;
                        foreach (var it in items)
                        {
                            string bg = idx % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            table.Cell().Background(bg).Padding(4).Text(it.Sku);
                            table.Cell().Background(bg).Padding(4).Text(it.ProductName);
                            table.Cell().Background(bg).Padding(4).Text(it.CategoryName);
                            table.Cell().Background(bg).Padding(4).Text(it.UnitName);
                            table.Cell().Background(bg).Padding(4).AlignRight().Text(it.CurrentStock.ToString("N2")).Bold();
                            table.Cell().Background(bg).Padding(4).AlignRight().Text(it.ReorderLevel.ToString("N0"));
                            if (!isCashier)
                            {
                                table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur}{it.CostPrice:N2}");
                                table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur}{it.StockValueAtCost:N2}").Bold();
                            }
                            idx++;
                        }
                    });
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Total items: {items.Count}").FontSize(8).FontColor("#94A3B8");
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });
        });

        byte[] pdfBytes = doc.GeneratePdf();
        return Task.FromResult(pdfBytes);
    }

    public Task<byte[]> GenerateValuationPdfAsync(IReadOnlyList<CategoryValuationDto> valuations, ShopSettingDto settings, CancellationToken cancellationToken = default)
    {
        var cur = settings.CurrencySymbol;
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                page.Header().Column(col =>
                {
                    col.Item().Text(settings.ShopName).FontSize(16).Bold().FontColor("#1E293B");
                    col.Item().Text("STOCK VALUATION BY CATEGORY").FontSize(12).SemiBold().FontColor("#475569");
                    col.Item().Text($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8).FontColor("#64748B");
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor("#CBD5E1");
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(60);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#F1F5F9").Padding(4).Text("Category").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Products").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Units").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Cost Value").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Retail Value").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Profit").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Margin %").Bold();
                        });

                        int idx = 0;
                        foreach (var v in valuations)
                        {
                            string bg = idx % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            table.Cell().Background(bg).Padding(4).Text(v.CategoryName);
                            table.Cell().Background(bg).Padding(4).AlignRight().Text(v.ProductCount.ToString());
                            table.Cell().Background(bg).Padding(4).AlignRight().Text(v.TotalUnits.ToString("N2"));
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur}{v.TotalCostValue:N2}");
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur}{v.TotalRetailValue:N2}");
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur}{v.PotentialProfit:N2}");
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{v.ProfitMarginPercent:N1}%");
                            idx++;
                        }

                        // Summary
                        table.Cell().Background("#F1F5F9").Padding(4).Text("TOTALS").Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text(valuations.Sum(v => v.ProductCount).ToString()).Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text(valuations.Sum(v => v.TotalUnits).ToString("N2")).Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur}{valuations.Sum(v => v.TotalCostValue):N2}").Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur}{valuations.Sum(v => v.TotalRetailValue):N2}").Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur}{valuations.Sum(v => v.PotentialProfit):N2}").Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).Text(string.Empty);
                    });
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Categories: {valuations.Count}").FontSize(8).FontColor("#94A3B8");
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });
        });

        byte[] pdfBytes = doc.GeneratePdf();
        return Task.FromResult(pdfBytes);
    }

    public Task<byte[]> GenerateSupplierLedgerPdfAsync(SupplierLedgerReportDto ledger, ShopSettingDto settings, CancellationToken cancellationToken = default)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                page.Header().Element(c => ComposeLedgerHeader(c, ledger, settings));
                page.Content().Element(c => ComposeLedgerContent(c, ledger, settings));
                page.Footer().Element(c => ComposeLedgerFooter(c));
            });
        });

        byte[] pdfBytes = doc.GeneratePdf();
        return Task.FromResult(pdfBytes);
    }

    private static void ComposePurchaseHeader(IContainer container, PurchaseDetailDto p, ShopSettingDto s)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(s.ShopName).FontSize(18).Bold().FontColor("#0F172A");
                    if (!string.IsNullOrWhiteSpace(s.Address)) c.Item().Text(s.Address).FontColor("#475569");
                    if (!string.IsNullOrWhiteSpace(s.Phone)) c.Item().Text($"Phone: {s.Phone}").FontColor("#475569");
                    if (!string.IsNullOrWhiteSpace(s.TaxNumber)) c.Item().Text($"Tax / VAT: {s.TaxNumber}").FontColor("#475569");
                });

                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().Text("PURCHASE INVOICE").FontSize(16).Bold().FontColor("#2563EB");
                    c.Item().Text($"Doc #: {p.PurchaseNumber}").Bold();
                    c.Item().Text($"Status: {p.Status}").FontColor(p.Status == Core.Enums.PurchaseStatus.Posted ? "#16A34A" : "#D97706");
                    c.Item().Text($"Date: {p.PurchaseDate:yyyy-MM-dd}");
                    if (p.DueDate.HasValue) c.Item().Text($"Due Date: {p.DueDate.Value:yyyy-MM-dd}");
                    if (!string.IsNullOrWhiteSpace(p.SupplierInvoiceNo)) c.Item().Text($"Supplier Inv: {p.SupplierInvoiceNo}");
                });
            });

            col.Item().PaddingTop(12).LineHorizontal(1).LineColor("#CBD5E1");

            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("VENDOR / SUPPLIER:").FontSize(9).Bold().FontColor("#64748B");
                    c.Item().Text(p.SupplierName).FontSize(12).Bold().FontColor("#0F172A");
                });
            });

            col.Item().PaddingTop(8);
        });
    }

    private static void ComposePurchaseContent(IContainer container, PurchaseDetailDto p, ShopSettingDto s)
    {
        string cur = s.CurrencySymbol;

        container.Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);  // #
                    columns.ConstantColumn(80);  // SKU
                    columns.RelativeColumn(3);   // Product Name
                    columns.RelativeColumn(1);   // Quantity
                    columns.RelativeColumn(1);   // Unit Cost
                    columns.RelativeColumn(1);   // Disc %
                    columns.RelativeColumn(1);   // Tax %
                    columns.RelativeColumn(1);   // Total
                });

                table.Header(header =>
                {
                    header.Cell().Background("#F1F5F9").Padding(5).Text("#").Bold();
                    header.Cell().Background("#F1F5F9").Padding(5).Text("SKU").Bold();
                    header.Cell().Background("#F1F5F9").Padding(5).Text("Product").Bold();
                    header.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text("Qty").Bold();
                    header.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text("Unit Cost").Bold();
                    header.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text("Disc %").Bold();
                    header.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text("Tax %").Bold();
                    header.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text("Total").Bold();
                });

                int idx = 1;
                foreach (var item in p.Items)
                {
                    string bg = idx % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
                    table.Cell().Background(bg).Padding(5).Text(idx.ToString());
                    table.Cell().Background(bg).Padding(5).Text(item.Sku);
                    table.Cell().Background(bg).Padding(5).Text(item.ProductName);
                    table.Cell().Background(bg).Padding(5).AlignRight().Text(item.Quantity.ToString("G29", CultureInfo.InvariantCulture));
                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{cur}{item.UnitCost:N2}");
                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.DiscountPercent:0.#}%");
                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.TaxPercent:0.#}%");
                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{cur}{item.LineTotal:N2}").Bold();
                    idx++;
                }
            });

            // Totals section
            col.Item().PaddingTop(12).AlignRight().Width(240).Column(tc =>
            {
                tc.Item().Row(r => { r.RelativeItem().Text("Sub Total:"); r.RelativeItem().AlignRight().Text($"{cur}{p.SubTotal:N2}"); });
                if (p.DiscountAmount > 0)
                    tc.Item().Row(r => { r.RelativeItem().Text("Invoice Discount:"); r.RelativeItem().AlignRight().Text($"-{cur}{p.DiscountAmount:N2}"); });
                if (p.TaxAmount > 0)
                    tc.Item().Row(r => { r.RelativeItem().Text("Tax Amount:"); r.RelativeItem().AlignRight().Text($"{cur}{p.TaxAmount:N2}"); });
                if (p.FreightAmount > 0)
                    tc.Item().Row(r => { r.RelativeItem().Text("Freight:"); r.RelativeItem().AlignRight().Text($"{cur}{p.FreightAmount:N2}"); });

                tc.Item().PaddingTop(4).LineHorizontal(1).LineColor("#94A3B8");
                tc.Item().PaddingTop(2).Row(r => { r.RelativeItem().Text("Grand Total:").FontSize(11).Bold(); r.RelativeItem().AlignRight().Text($"{cur}{p.GrandTotal:N2}").FontSize(12).Bold().FontColor("#2563EB"); });
                tc.Item().Row(r => { r.RelativeItem().Text("Paid Amount:"); r.RelativeItem().AlignRight().Text($"{cur}{p.PaidAmount:N2}"); });
                tc.Item().Row(r => { r.RelativeItem().Text("Balance Due:").Bold(); r.RelativeItem().AlignRight().Text($"{cur}{p.Balance:N2}").Bold().FontColor(p.Balance > 0 ? "#DC2626" : "#16A34A"); });
            });

            if (!string.IsNullOrWhiteSpace(p.Notes))
            {
                col.Item().PaddingTop(16).Column(nc =>
                {
                    nc.Item().Text("Notes:").Bold().FontSize(9).FontColor("#64748B");
                    nc.Item().Text(p.Notes).FontSize(9).FontColor("#334155");
                });
            }
        });
    }

    private static void ComposePurchaseFooter(IContainer container, PurchaseDetailDto p)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text($"Generated on {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8).FontColor("#94A3B8");
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }

    private static void ComposeLedgerHeader(IContainer container, SupplierLedgerReportDto l, ShopSettingDto s)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(s.ShopName).FontSize(16).Bold().FontColor("#0F172A");
                    if (!string.IsNullOrWhiteSpace(s.Phone)) c.Item().Text($"Phone: {s.Phone}").FontColor("#475569");
                });

                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().Text("SUPPLIER STATEMENT").FontSize(15).Bold().FontColor("#2563EB");
                    c.Item().Text($"Period: {l.FromDate:yyyy-MM-dd} to {l.ToDate:yyyy-MM-dd}").Bold();
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(1).LineColor("#CBD5E1");

            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("SUPPLIER ACCOUNT:").FontSize(8).Bold().FontColor("#64748B");
                    c.Item().Text(l.SupplierName).FontSize(11).Bold().FontColor("#0F172A");
                });

                row.RelativeItem().AlignRight().Column(c =>
                {
                    string cur = s.CurrencySymbol;
                    c.Item().Text($"Closing Balance: {cur}{l.ClosingBalance:N2}").FontSize(11).Bold().FontColor(l.ClosingBalance > 0 ? "#DC2626" : "#16A34A");
                });
            });

            col.Item().PaddingTop(8);
        });
    }

    private static void ComposeLedgerContent(IContainer container, SupplierLedgerReportDto l, ShopSettingDto s)
    {
        string cur = s.CurrencySymbol;

        container.Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(65);  // Date
                    columns.ConstantColumn(80);  // Type
                    columns.RelativeColumn(2);   // Reference
                    columns.RelativeColumn(2);   // Notes
                    columns.RelativeColumn(1);   // Debit
                    columns.RelativeColumn(1);   // Credit
                    columns.RelativeColumn(1);   // Balance
                });

                table.Header(header =>
                {
                    header.Cell().Background("#F1F5F9").Padding(4).Text("Date").Bold();
                    header.Cell().Background("#F1F5F9").Padding(4).Text("Type").Bold();
                    header.Cell().Background("#F1F5F9").Padding(4).Text("Reference").Bold();
                    header.Cell().Background("#F1F5F9").Padding(4).Text("Notes").Bold();
                    header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Debit (+Purch)").Bold();
                    header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Credit (-Paid)").Bold();
                    header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Balance").Bold();
                });

                // Row 1: Opening balance row
                table.Cell().Background("#EFF6FF").Padding(4).Text(l.FromDate.ToString("yyyy-MM-dd"));
                table.Cell().Background("#EFF6FF").Padding(4).Text("Opening").Bold();
                table.Cell().Background("#EFF6FF").Padding(4).Text("Balance Forward");
                table.Cell().Background("#EFF6FF").Padding(4).Text(string.Empty);
                table.Cell().Background("#EFF6FF").Padding(4).AlignRight().Text(string.Empty);
                table.Cell().Background("#EFF6FF").Padding(4).AlignRight().Text(string.Empty);
                table.Cell().Background("#EFF6FF").Padding(4).AlignRight().Text($"{cur}{l.OpeningBalance:N2}").Bold();

                int idx = 2;
                foreach (var entry in l.Entries)
                {
                    string bg = idx % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                    table.Cell().Background(bg).Padding(4).Text(entry.Date.ToString("yyyy-MM-dd"));
                    table.Cell().Background(bg).Padding(4).Text(entry.Type);
                    table.Cell().Background(bg).Padding(4).Text(entry.Reference);
                    table.Cell().Background(bg).Padding(4).Text(entry.Notes ?? string.Empty);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(entry.Debit > 0 ? $"{cur}{entry.Debit:N2}" : string.Empty);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(entry.Credit > 0 ? $"{cur}{entry.Credit:N2}" : string.Empty);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur}{entry.RunningBalance:N2}").Bold();
                    idx++;
                }

                // Summary footer
                table.Cell().Background("#F1F5F9").Padding(4).Text("TOTALS").Bold();
                table.Cell().Background("#F1F5F9").Padding(4).Text(string.Empty);
                table.Cell().Background("#F1F5F9").Padding(4).Text(string.Empty);
                table.Cell().Background("#F1F5F9").Padding(4).Text(string.Empty);
                table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur}{l.TotalDebits:N2}").Bold();
                table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur}{l.TotalCredits:N2}").Bold();
                table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur}{l.ClosingBalance:N2}").Bold();
            });
        });
    }

    private static void ComposeLedgerFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text($"Generated on {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8).FontColor("#94A3B8");
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }
}

