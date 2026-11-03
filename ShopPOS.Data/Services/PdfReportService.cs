using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Services;

public class PdfReportService : IPdfReportService
{
    static PdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
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

    public Task<byte[]> GenerateStockOverviewPdfAsync(IReadOnlyList<StockOverviewItemDto> items, ShopSettingDto settings, bool isCashier = false, CancellationToken cancellationToken = default)
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
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(65);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#F1F5F9").Padding(4).Text("SKU").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).Text("Product").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).Text("Category").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).Text("Unit").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Stock").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Alert").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Cost").Bold();
                            header.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text("Total Value").Bold();
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
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur} {it.CostPrice:N2}");
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur} {it.StockValueAtCost:N2}").Bold();
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
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur} {v.TotalCostValue:N2}");
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur} {v.TotalRetailValue:N2}");
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur} {v.PotentialProfit:N2}");
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{v.ProfitMarginPercent:N1}%");
                            idx++;
                        }

                        // Summary
                        table.Cell().Background("#F1F5F9").Padding(4).Text("TOTALS").Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text(valuations.Sum(v => v.ProductCount).ToString()).Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text(valuations.Sum(v => v.TotalUnits).ToString("N2")).Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur} {valuations.Sum(v => v.TotalCostValue):N2}").Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur} {valuations.Sum(v => v.TotalRetailValue):N2}").Bold();
                        table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur} {valuations.Sum(v => v.PotentialProfit):N2}").Bold();
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
                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{cur} {item.UnitCost:N2}");
                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.DiscountPercent:0.#}%");
                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.TaxPercent:0.#}%");
                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{cur} {item.LineTotal:N2}").Bold();
                    idx++;
                }
            });

            // Totals section
            col.Item().PaddingTop(12).AlignRight().Width(240).Column(tc =>
            {
                tc.Item().Row(r => { r.RelativeItem().Text("Sub Total:"); r.RelativeItem().AlignRight().Text($"{cur} {p.SubTotal:N2}"); });
                if (p.DiscountAmount > 0)
                    tc.Item().Row(r => { r.RelativeItem().Text("Invoice Discount:"); r.RelativeItem().AlignRight().Text($"-{cur} {p.DiscountAmount:N2}"); });
                if (p.TaxAmount > 0)
                    tc.Item().Row(r => { r.RelativeItem().Text("Tax Amount:"); r.RelativeItem().AlignRight().Text($"{cur} {p.TaxAmount:N2}"); });
                if (p.FreightAmount > 0)
                    tc.Item().Row(r => { r.RelativeItem().Text("Freight:"); r.RelativeItem().AlignRight().Text($"{cur} {p.FreightAmount:N2}"); });

                tc.Item().PaddingTop(4).LineHorizontal(1).LineColor("#94A3B8");
                tc.Item().PaddingTop(2).Row(r => { r.RelativeItem().Text("Grand Total:").FontSize(11).Bold(); r.RelativeItem().AlignRight().Text($"{cur} {p.GrandTotal:N2}").FontSize(12).Bold().FontColor("#2563EB"); });
                tc.Item().Row(r => { r.RelativeItem().Text("Paid Amount:"); r.RelativeItem().AlignRight().Text($"{cur} {p.PaidAmount:N2}"); });
                tc.Item().Row(r => { r.RelativeItem().Text("Balance Due:").Bold(); r.RelativeItem().AlignRight().Text($"{cur} {p.Balance:N2}").Bold().FontColor(p.Balance > 0 ? "#DC2626" : "#16A34A"); });
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
                    c.Item().Text($"Closing Balance: {cur} {l.ClosingBalance:N2}").FontSize(11).Bold().FontColor(l.ClosingBalance > 0 ? "#DC2626" : "#16A34A");
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
                table.Cell().Background("#EFF6FF").Padding(4).AlignRight().Text($"{cur} {l.OpeningBalance:N2}").Bold();

                int idx = 2;
                foreach (var entry in l.Entries)
                {
                    string bg = idx % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                    table.Cell().Background(bg).Padding(4).Text(entry.Date.ToString("yyyy-MM-dd"));
                    table.Cell().Background(bg).Padding(4).Text(entry.Type);
                    table.Cell().Background(bg).Padding(4).Text(entry.Reference);
                    table.Cell().Background(bg).Padding(4).Text(entry.Notes ?? string.Empty);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(entry.Debit > 0 ? $"{cur} {entry.Debit:N2}" : string.Empty);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(entry.Credit > 0 ? $"{cur} {entry.Credit:N2}" : string.Empty);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur} {entry.RunningBalance:N2}").Bold();
                    idx++;
                }

                // Summary footer
                table.Cell().Background("#F1F5F9").Padding(4).Text("TOTALS").Bold();
                table.Cell().Background("#F1F5F9").Padding(4).Text(string.Empty);
                table.Cell().Background("#F1F5F9").Padding(4).Text(string.Empty);
                table.Cell().Background("#F1F5F9").Padding(4).Text(string.Empty);
                table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur} {l.TotalDebits:N2}").Bold();
                table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur} {l.TotalCredits:N2}").Bold();
                table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur} {l.ClosingBalance:N2}").Bold();
            });
        });
    }

    public Task<byte[]> GenerateSaleReceiptPdfAsync(SaleDetailDto sale, ShopSettingDto settings, CancellationToken cancellationToken = default)
    {
        var cur = settings.CurrencySymbol;
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(settings.ShopName).FontSize(14).Bold();
                    if (!string.IsNullOrWhiteSpace(settings.Address))
                    {
                        col.Item().AlignCenter().Text(settings.Address).FontSize(8).FontColor("#64748B");
                    }
                    if (!string.IsNullOrWhiteSpace(settings.Phone))
                    {
                        col.Item().AlignCenter().Text($"Tel: {settings.Phone}").FontSize(8).FontColor("#64748B");
                    }
                    col.Item().PaddingTop(6).AlignCenter().Text("SALES RECEIPT").FontSize(11).Bold();
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text($"Receipt: {sale.SaleNumber}").Bold();
                        row.RelativeItem().AlignRight().Text($"Date: {sale.SaleDate:yyyy-MM-dd HH:mm}");
                    });

                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor("#CBD5E1");
                });

                page.Content().PaddingTop(6).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(24);
                            cols.RelativeColumn(3);
                            cols.ConstantColumn(40);
                            cols.ConstantColumn(55);
                            cols.ConstantColumn(60);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("#").Bold();
                            header.Cell().Text("Item").Bold();
                            header.Cell().AlignRight().Text("Qty").Bold();
                            header.Cell().AlignRight().Text("Price").Bold();
                            header.Cell().AlignRight().Text("Total").Bold();
                        });

                        int idx = 1;
                        foreach (var item in sale.Items)
                        {
                            table.Cell().Text(idx.ToString());
                            table.Cell().Text(item.ProductName);
                            table.Cell().AlignRight().Text($"{item.Quantity:0.##}");
                            table.Cell().AlignRight().Text($"{cur} {item.UnitPrice:F2}");
                            table.Cell().AlignRight().Text($"{cur} {item.LineTotal:F2}");
                            idx++;
                        }
                    });

                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E2E8F0");

                    col.Item().PaddingTop(4).AlignRight().Column(totals =>
                    {
                        totals.Item().Text($"Subtotal: {cur} {sale.SubTotal:F2}");
                        if (sale.LineDiscountTotal > 0 || sale.BillDiscountAmount > 0)
                        {
                            totals.Item().Text($"Discount: -{cur} {(sale.LineDiscountTotal + sale.BillDiscountAmount):F2}").FontColor("#DC2626");
                        }
                        if (sale.TaxAmount > 0)
                        {
                            totals.Item().Text($"Tax: {cur} {sale.TaxAmount:F2}");
                        }
                        if (sale.RoundOff != 0)
                        {
                            totals.Item().Text($"Round-off: {cur} {sale.RoundOff:F2}");
                        }
                        totals.Item().PaddingTop(2).Text($"Grand Total: {cur} {sale.GrandTotal:F2}").FontSize(12).Bold();

                        foreach (var p in sale.Payments)
                        {
                            totals.Item().Text($"Paid ({p.Method}): {cur} {p.Amount:F2}");
                        }
                        if (sale.ChangeAmount > 0)
                        {
                            totals.Item().Text($"Change Due: {cur} {sale.ChangeAmount:F2}").Bold().FontColor("#16A34A");
                        }
                        if (sale.DueAmount > 0)
                        {
                            totals.Item().Text($"Balance Due: {cur} {sale.DueAmount:F2}").Bold().FontColor("#EA580C");
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(settings.ReceiptFooterText))
                    {
                        col.Item().PaddingTop(12).AlignCenter().Text(settings.ReceiptFooterText).FontSize(8).Italic();
                    }
                });

                page.Footer().AlignCenter().Text("Thank you for shopping with us!").FontSize(8).FontColor("#94A3B8");
            });
        });

        return Task.FromResult(doc.GeneratePdf());
    }

    public Task<byte[]> GenerateSalesListPdfAsync(IReadOnlyList<SaleSummaryDto> sales, ShopSettingDto settings, CancellationToken cancellationToken = default)
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
                    col.Item().Text("SALES SUMMARY REPORT").FontSize(12).SemiBold().FontColor("#475569");
                    col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm} | Total Records: {sales.Count}").FontSize(8).FontColor("#64748B");
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor("#CBD5E1");
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(80);
                        cols.ConstantColumn(95);
                        cols.RelativeColumn(2);
                        cols.ConstantColumn(40);
                        cols.ConstantColumn(65);
                        cols.ConstantColumn(65);
                        cols.ConstantColumn(65);
                        cols.ConstantColumn(60);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background("#1E293B").Padding(4).Text("Doc #").FontColor("#FFFFFF").Bold();
                        header.Cell().Background("#1E293B").Padding(4).Text("Date").FontColor("#FFFFFF").Bold();
                        header.Cell().Background("#1E293B").Padding(4).Text("Payments").FontColor("#FFFFFF").Bold();
                        header.Cell().Background("#1E293B").Padding(4).AlignRight().Text("Items").FontColor("#FFFFFF").Bold();
                        header.Cell().Background("#1E293B").Padding(4).AlignRight().Text("Total").FontColor("#FFFFFF").Bold();
                        header.Cell().Background("#1E293B").Padding(4).AlignRight().Text("Paid").FontColor("#FFFFFF").Bold();
                        header.Cell().Background("#1E293B").Padding(4).AlignRight().Text("Due").FontColor("#FFFFFF").Bold();
                        header.Cell().Background("#1E293B").Padding(4).Text("Status").FontColor("#FFFFFF").Bold();
                    });

                    int idx = 0;
                    foreach (var s in sales)
                    {
                        string bg = idx % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                        table.Cell().Background(bg).Padding(4).Text(s.SaleNumber).Bold();
                        table.Cell().Background(bg).Padding(4).Text(s.SaleDate.ToString("yyyy-MM-dd HH:mm"));
                        table.Cell().Background(bg).Padding(4).Text(s.PaymentMethodsSummary);
                        table.Cell().Background(bg).Padding(4).AlignRight().Text(s.ItemCount.ToString());
                        table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur} {s.GrandTotal:F2}").Bold();
                        table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur} {s.PaidAmount:F2}");
                        table.Cell().Background(bg).Padding(4).AlignRight().Text($"{cur} {s.DueAmount:F2}");
                        table.Cell().Background(bg).Padding(4).Text(s.Status.ToString());
                        idx++;
                    }

                    // Summary footer
                    table.Cell().Background("#F1F5F9").Padding(4).Text("TOTALS").Bold();
                    table.Cell().Background("#F1F5F9").Padding(4).Text(string.Empty);
                    table.Cell().Background("#F1F5F9").Padding(4).Text(string.Empty);
                    table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text(sales.Sum(s => s.ItemCount).ToString()).Bold();
                    table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur} {sales.Sum(s => s.GrandTotal):F2}").Bold();
                    table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur} {sales.Sum(s => s.PaidAmount):F2}").Bold();
                    table.Cell().Background("#F1F5F9").Padding(4).AlignRight().Text($"{cur} {sales.Sum(s => s.DueAmount):F2}").Bold();
                    table.Cell().Background("#F1F5F9").Padding(4).Text(string.Empty);
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Page count verification").FontSize(8).FontColor("#94A3B8");
                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });
        });

        return Task.FromResult(doc.GeneratePdf());
    }

    public Task<byte[]> GenerateReceiptPdfAsync(ReceiptDocument document, CancellationToken cancellationToken = default)
    {
        var doc = CreateReceiptQuestDocument(document);
        return Task.FromResult(doc.GeneratePdf());
    }

    public Task<byte[]> GenerateA4InvoicePdfAsync(ReceiptDocument document, CancellationToken cancellationToken = default)
    {
        var doc = CreateA4InvoiceQuestDocument(document);
        return Task.FromResult(doc.GeneratePdf());
    }

    public IReadOnlyList<byte[]> RenderDocumentPageImages(ReceiptDocument document)
    {
        var doc = document.PaperWidthMm > 100
            ? CreateA4InvoiceQuestDocument(document)
            : CreateReceiptQuestDocument(document);

        return doc.GenerateImages().ToList();
    }

    private static IDocument CreateReceiptQuestDocument(ReceiptDocument doc)
    {
        string cur = doc.CurrencySymbol;
        bool is58 = doc.PaperWidthMm <= 58;
        float fontSize = is58 ? 7.5f : 8.5f;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(is58 ? 57 : 80, Unit.Millimetre);
                page.Margin(is58 ? 4 : 8, Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(fontSize).FontFamily("Segoe UI"));

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().Text(doc.ShopName).FontSize(is58 ? 11 : 13).Bold().FontColor("#0F172A");
                    if (!string.IsNullOrWhiteSpace(doc.ShopAddress))
                    {
                        col.Item().AlignCenter().Text(doc.ShopAddress).FontSize(fontSize - 1).FontColor("#475569");
                    }
                    if (!string.IsNullOrWhiteSpace(doc.ShopPhone))
                    {
                        col.Item().AlignCenter().Text($"Tel: {doc.ShopPhone}").FontSize(fontSize - 1).FontColor("#475569");
                    }
                    if (!string.IsNullOrWhiteSpace(doc.ShopTaxNumber))
                    {
                        col.Item().AlignCenter().Text($"Tax Reg: {doc.ShopTaxNumber}").FontSize(fontSize - 1).FontColor("#475569");
                    }

                    col.Item().PaddingTop(4).AlignCenter().Text(doc.DocumentTitle).FontSize(fontSize + 1).Bold().FontColor("#1E293B");

                    if (doc.IsDuplicateCopy)
                    {
                        col.Item().PaddingTop(3).Border(1).BorderColor("#EF4444").Background("#FEF2F2").Padding(3).AlignCenter().Column(dup =>
                        {
                            dup.Item().AlignCenter().Text("*** DUPLICATE COPY ***").FontSize(fontSize).Bold().FontColor("#DC2626");
                            dup.Item().AlignCenter().Text($"Reprint #{Math.Max(1, doc.ReprintCount)} on {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(fontSize - 1.5f).FontColor("#991B1B");
                        });
                    }

                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#CBD5E1");

                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.RelativeItem().Text($"Doc: {doc.InvoiceNumber}").Bold();
                        r.RelativeItem().AlignRight().Text(doc.DateTime.ToString("yyyy-MM-dd HH:mm"));
                    });
                    col.Item().Text($"Cashier: {doc.CashierName}");


                    col.Item().PaddingTop(2).LineHorizontal(1).LineColor("#CBD5E1");

                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(1.3f);
                        });

                        foreach (var item in doc.Lines)
                        {
                            table.Cell().Column(ic =>
                            {
                                ic.Item().Text(item.ItemName).Bold();
                                string sub = $"{item.Quantity:0.##} x {cur} {item.UnitPrice:F2}";
                                if (item.DiscountAmount > 0) sub += $" (-{cur} {item.DiscountAmount:F2})";
                                ic.Item().Text(sub).FontSize(fontSize - 1).FontColor("#64748B");
                            });

                            table.Cell().AlignRight().Text($"{cur} {item.LineTotal:F2}").Bold();
                        }
                    });

                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#CBD5E1");

                    col.Item().PaddingTop(2).Column(totals =>
                    {
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Subtotal:");
                            r.RelativeItem().AlignRight().Text($"{cur} {doc.SubTotal:F2}");
                        });

                        if (doc.TotalDiscount > 0)
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Total Discount:").FontColor("#DC2626");
                                r.RelativeItem().AlignRight().Text($"-{cur} {doc.TotalDiscount:F2}").FontColor("#DC2626");
                            });
                        }

                        if (doc.TaxAmount > 0)
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Tax / VAT:");
                                r.RelativeItem().AlignRight().Text($"{cur} {doc.TaxAmount:F2}");
                            });
                        }

                        if (doc.RoundOff != 0)
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Round-off:");
                                r.RelativeItem().AlignRight().Text($"{cur} {doc.RoundOff:F2}");
                            });
                        }

                        totals.Item().PaddingTop(2).LineHorizontal(1).LineColor("#94A3B8");

                        totals.Item().PaddingTop(2).Row(r =>
                        {
                            r.RelativeItem().Text("GRAND TOTAL:").FontSize(fontSize + 2).Bold();
                            r.RelativeItem().AlignRight().Text($"{cur} {doc.GrandTotal:F2}").FontSize(fontSize + 2).Bold();
                        });

                        totals.Item().PaddingTop(2).LineHorizontal(1).LineColor("#CBD5E1");

                        foreach (var p in doc.Payments)
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text($"Paid ({p.Method}):");
                                r.RelativeItem().AlignRight().Text($"{cur} {p.Amount:F2}");
                            });
                        }

                        if (doc.ChangeAmount > 0)
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Change Due:").Bold().FontColor("#16A34A");
                                r.RelativeItem().AlignRight().Text($"{cur} {doc.ChangeAmount:F2}").Bold().FontColor("#16A34A");
                            });
                        }

                        if (doc.DueAmount > 0)
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Balance Due:").Bold().FontColor("#EA580C");
                                r.RelativeItem().AlignRight().Text($"{cur} {doc.DueAmount:F2}").Bold().FontColor("#EA580C");
                            });

                        }
                    });

                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E2E8F0");
                    if (!string.IsNullOrWhiteSpace(doc.FooterText))
                    {
                        col.Item().PaddingTop(4).AlignCenter().Text(doc.FooterText).FontSize(fontSize - 1).Italic().FontColor("#64748B");
                    }

                    if (!string.IsNullOrWhiteSpace(doc.BarcodeOrQrValue))
                    {
                        col.Item().PaddingTop(4).AlignCenter().Border(1).BorderColor("#CBD5E1").Padding(3).Text($"* {doc.BarcodeOrQrValue} *").FontFamily("Consolas").FontSize(fontSize);
                    }

                    col.Item().PaddingTop(4).AlignCenter().Text("Thank you for your visit!").FontSize(fontSize - 1.5f).FontColor("#94A3B8");
                });
            });
        });
    }

    private static IDocument CreateA4InvoiceQuestDocument(ReceiptDocument doc)
    {
        string cur = doc.CurrencySymbol;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30, Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Segoe UI"));

                page.Header().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Column(sc =>
                        {
                            sc.Item().Text(doc.ShopName).FontSize(18).Bold().FontColor("#0F172A");
                            if (!string.IsNullOrWhiteSpace(doc.ShopAddress)) sc.Item().Text(doc.ShopAddress).FontColor("#475569");
                            if (!string.IsNullOrWhiteSpace(doc.ShopPhone)) sc.Item().Text($"Phone: {doc.ShopPhone}").FontColor("#475569");
                            if (!string.IsNullOrWhiteSpace(doc.ShopTaxNumber)) sc.Item().Text($"Tax / VAT Reg: {doc.ShopTaxNumber}").Bold().FontColor("#334155");
                        });

                        r.RelativeItem().AlignRight().Column(ic =>
                        {
                            ic.Item().Text("TAX INVOICE").FontSize(20).Bold().FontColor("#2563EB");
                            ic.Item().Text($"Invoice #: {doc.InvoiceNumber}").Bold().FontSize(11);
                            ic.Item().Text($"Date: {doc.DateTime:yyyy-MM-dd HH:mm}");
                            ic.Item().Text($"Cashier: {doc.CashierName}");
                            if (doc.IsDuplicateCopy)
                            {
                                ic.Item().Text("*** DUPLICATE COPY ***").Bold().FontColor("#DC2626");
                            }
                        });
                    });

                    col.Item().PaddingTop(12).LineHorizontal(1.5f).LineColor("#CBD5E1");

                    col.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Border(1).BorderColor("#E2E8F0").Background("#F8FAFC").Padding(8).Column(bc =>
                        {
                            bc.Item().Text("BILL TO:").FontSize(8.5f).Bold().FontColor("#64748B");
                            bc.Item().Text("Walk-in Counter Sale").FontSize(11).Bold().FontColor("#0F172A");
                        });
                    });

                    col.Item().PaddingTop(10);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(28);  // #
                            cols.ConstantColumn(80);  // SKU
                            cols.RelativeColumn(3);   // Description
                            cols.RelativeColumn(1);   // Quantity
                            cols.RelativeColumn(1.2f); // Unit Price
                            cols.RelativeColumn(1);   // Disc
                            cols.RelativeColumn(1);   // Tax %
                            cols.RelativeColumn(1.3f); // Total
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#F1F5F9").Padding(5).Text("#").Bold();
                            header.Cell().Background("#F1F5F9").Padding(5).Text("SKU").Bold();
                            header.Cell().Background("#F1F5F9").Padding(5).Text("Description").Bold();
                            header.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text("Qty").Bold();
                            header.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text("Unit Price").Bold();
                            header.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text("Disc").Bold();
                            header.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text("Tax %").Bold();
                            header.Cell().Background("#F1F5F9").Padding(5).AlignRight().Text("Total").Bold();
                        });

                        int idx = 1;
                        foreach (var item in doc.Lines)
                        {
                            string bg = idx % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
                            table.Cell().Background(bg).Padding(5).Text(idx.ToString());
                            table.Cell().Background(bg).Padding(5).Text(item.Sku);
                            table.Cell().Background(bg).Padding(5).Text(item.ItemName);
                            table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.Quantity:0.##}");
                            table.Cell().Background(bg).Padding(5).AlignRight().Text($"{cur} {item.UnitPrice:F2}");
                            table.Cell().Background(bg).Padding(5).AlignRight().Text(item.DiscountAmount > 0 ? $"-{cur} {item.DiscountAmount:F2}" : "0.00");
                            table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.TaxPercent:0.#}%");
                            table.Cell().Background(bg).Padding(5).AlignRight().Text($"{cur} {item.LineTotal:F2}").Bold();
                            idx++;
                        }
                    });

                    col.Item().PaddingTop(12).Row(r =>
                    {
                        r.RelativeItem(3).Column(pc =>
                        {
                            pc.Item().Text("PAYMENT DETAILS").FontSize(9).Bold().FontColor("#64748B");
                            foreach (var p in doc.Payments)
                            {
                                pc.Item().Text($"• {p.Method}: {cur} {p.Amount:F2} {(p.Reference != null ? $"({p.Reference})" : "")}");
                            }
                            if (doc.ChangeAmount > 0)
                            {
                                pc.Item().Text($"• Change Returned: {cur} {doc.ChangeAmount:F2}").FontColor("#16A34A").Bold();
                            }
                            if (doc.DueAmount > 0)
                            {
                                pc.Item().Text($"• Outstanding Balance Due: {cur} {doc.DueAmount:F2}").FontColor("#EA580C").Bold();
                                // no customer balance
                            }

                            if (!string.IsNullOrWhiteSpace(doc.FooterText))
                            {
                                pc.Item().PaddingTop(8).Text(doc.FooterText).Italic().FontColor("#475569");
                            }
                        });

                        r.RelativeItem(2).Border(1).BorderColor("#CBD5E1").Background("#F8FAFC").Padding(8).Column(tc =>
                        {
                            tc.Item().Row(tr =>
                            {
                                tr.RelativeItem().Text("Subtotal:");
                                tr.RelativeItem().AlignRight().Text($"{cur} {doc.SubTotal:F2}");
                            });

                            if (doc.TotalDiscount > 0)
                            {
                                tc.Item().Row(tr =>
                                {
                                    tr.RelativeItem().Text("Discounts:").FontColor("#DC2626");
                                    tr.RelativeItem().AlignRight().Text($"-{cur} {doc.TotalDiscount:F2}").FontColor("#DC2626");
                                });
                            }

                            if (doc.TaxAmount > 0)
                            {
                                tc.Item().Row(tr =>
                                {
                                    tr.RelativeItem().Text("Tax / VAT:");
                                    tr.RelativeItem().AlignRight().Text($"{cur} {doc.TaxAmount:F2}");
                                });
                            }

                            if (doc.RoundOff != 0)
                            {
                                tc.Item().Row(tr =>
                                {
                                    tr.RelativeItem().Text("Round-off:");
                                    tr.RelativeItem().AlignRight().Text($"{cur} {doc.RoundOff:F2}");
                                });
                            }

                            tc.Item().PaddingTop(3).LineHorizontal(1).LineColor("#94A3B8");

                            tc.Item().PaddingTop(2).Row(tr =>
                            {
                                tr.RelativeItem().Text("GRAND TOTAL:").Bold().FontSize(12);
                                tr.RelativeItem().AlignRight().Text($"{cur} {doc.GrandTotal:F2}").Bold().FontSize(12).FontColor("#2563EB");
                            });

                            tc.Item().Row(tr =>
                            {
                                tr.RelativeItem().Text("Paid Amount:").Bold();
                                tr.RelativeItem().AlignRight().Text($"{cur} {doc.PaidAmount:F2}").Bold();
                            });
                        });
                    });

                    col.Item().PaddingTop(24).Row(r =>
                    {
                        r.RelativeItem().Column(sc =>
                        {
                            sc.Item().LineHorizontal(1).LineColor("#94A3B8");
                            sc.Item().PaddingTop(2).AlignCenter().Text("Customer Signature").FontSize(8.5f).FontColor("#64748B");
                        });
                        r.ConstantItem(50);
                        r.RelativeItem().Column(sc =>
                        {
                            sc.Item().LineHorizontal(1).LineColor("#94A3B8");
                            sc.Item().PaddingTop(2).AlignCenter().Text("Authorized Shop Signature").FontSize(8.5f).FontColor("#64748B");
                        });
                    });
                });

                page.Footer().Row(r =>
                {
                    r.RelativeItem().Text($"Generated on {DateTime.Now:yyyy-MM-dd HH:mm} - Offline Single-User POS").FontSize(8).FontColor("#94A3B8");
                    r.RelativeItem().AlignRight().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
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

