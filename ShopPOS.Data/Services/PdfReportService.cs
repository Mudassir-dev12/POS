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

    private const string BabyEmblemSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 200 200"" width=""200"" height=""200"">
  <circle cx=""100"" cy=""100"" r=""92"" fill=""none"" stroke=""#000000"" stroke-width=""8""/>
  <circle cx=""82"" cy=""52"" r=""14"" fill=""#000000""/>
  <path d=""M 70 70 C 65 85, 75 95, 84 95 C 92 95, 96 85, 100 80"" stroke=""#000000"" stroke-width=""8"" stroke-linecap=""round"" fill=""none""/>
  <circle cx=""118"" cy=""52"" r=""14"" fill=""#000000""/>
  <path d=""M 130 70 C 135 85, 125 95, 116 95 C 108 95, 104 85, 100 80"" stroke=""#000000"" stroke-width=""8"" stroke-linecap=""round"" fill=""none""/>
  <circle cx=""100"" cy=""80"" r=""4"" fill=""#000000""/>
  <ellipse cx=""100"" cy=""132"" rx=""72"" ry=""34"" fill=""#000000""/>
  <text x=""100"" y=""126"" font-family=""Arial, Helvetica, sans-serif"" font-size=""24"" font-weight=""900"" fill=""#ffffff"" text-anchor=""middle"" letter-spacing=""2"">GIFTO</text>
  <text x=""100"" y=""152"" font-family=""Arial, Helvetica, sans-serif"" font-size=""22"" font-weight=""900"" fill=""#ffffff"" text-anchor=""middle"" letter-spacing=""2"">BABY</text>
</svg>";

    private static IDocument CreateReceiptQuestDocument(ReceiptDocument doc)
    {
        string cur = doc.CurrencySymbol;
        bool is58 = doc.PaperWidthMm <= 58;
        float baseFontSize = is58 ? 8f : 9f;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(is58 ? 57 : 80, Unit.Millimetre);
                page.Margin(is58 ? 4 : 8, Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(baseFontSize).FontFamily("Arial").FontColor("#000000"));

                page.Content().Column(col =>
                {
                    // 1. Top Logo Emblem in crisp Vector SVG
                    if (doc.LogoImageBytes != null && doc.LogoImageBytes.Length > 0)
                    {
                        col.Item().PaddingBottom(4).AlignCenter().MaxWidth(is58 ? 55 : 70).Image(doc.LogoImageBytes);
                    }
                    else
                    {
                        col.Item().PaddingBottom(4).AlignCenter().Width(is58 ? 55 : 70).Height(is58 ? 55 : 70).Svg(BabyEmblemSvg);
                    }

                    // 2. Store Name
                    string shopName = !string.IsNullOrWhiteSpace(doc.ShopName) ? doc.ShopName : "Gifto Baby World";
                    col.Item().AlignCenter().Text(shopName).FontSize(is58 ? 13 : 16).Bold();

                    // Address with pin
                    string address = !string.IsNullOrWhiteSpace(doc.ShopAddress)
                        ? doc.ShopAddress
                        : "Shop No. B-27, Hydri Bara Market, Block H, North Nazimabad (Near Taimoria Masjid), Karachi, Pakistan";
                    col.Item().PaddingTop(2).AlignCenter().Text(t =>
                    {
                        // Location pin
                        t.Span(address).FontSize(baseFontSize - 0.5f);
                    });

                    // 2 Contact Phone Numbers
                    string phone1 = !string.IsNullOrWhiteSpace(doc.ShopPhone) ? doc.ShopPhone : "0335-5558359";
                    string phone2 = !string.IsNullOrWhiteSpace(doc.ShopPhone2) ? doc.ShopPhone2 : "0334-5373743";
                    col.Item().PaddingTop(2).AlignCenter().Text(t =>
                    {
                        // Phone icon
                        t.Span($"{phone1}  |  {phone2}").FontSize(baseFontSize).Bold();
                    });

                    if (doc.IsDuplicateCopy)
                    {
                        col.Item().PaddingTop(2).AlignCenter().Text("*** DUPLICATE COPY ***").FontSize(baseFontSize).Bold();
                    }

                    // 3. Metadata 3-Column Header: ORDER ID, DATE, TIME
                    col.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text("ORDER ID").FontSize(baseFontSize - 1).Bold();
                            c.Item().AlignCenter().Text(doc.InvoiceNumber).FontSize(baseFontSize).Bold();
                        });

                        r.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text("DATE").FontSize(baseFontSize - 1).Bold();
                            c.Item().AlignCenter().Text(doc.DateTime.ToString("dd-MMM-yyyy")).FontSize(baseFontSize).Bold();
                        });

                        r.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text("TIME").FontSize(baseFontSize - 1).Bold();
                            c.Item().AlignCenter().Text(doc.DateTime.ToString("HH:mm")).FontSize(baseFontSize).Bold();
                        });
                    });

                    // Divider
                    col.Item().PaddingTop(4).PaddingBottom(3).LineHorizontal(1).LineColor("#000000");

                    // 4. Customer Name and Contact No
                    col.Item().PaddingVertical(1).Column(cust =>
                    {
                        string custName = !string.IsNullOrWhiteSpace(doc.CustomerName) ? doc.CustomerName : "Mudassir";
                        string custPhone = !string.IsNullOrWhiteSpace(doc.CustomerPhone) ? doc.CustomerPhone : "0312-3456789";

                        cust.Item().Text(t =>
                        {
                            t.Span("Customer Name: ").Bold();
                            t.Span(custName);
                        });

                        cust.Item().Text(t =>
                        {
                            t.Span("Contact No: ").Bold();
                            t.Span(custPhone);
                        });
                    });

                    // Divider
                    col.Item().PaddingTop(3).PaddingBottom(3).LineHorizontal(1).LineColor("#000000");

                    // 5. Purchase Details Table: QTY, DESCRIPTION, UNIT PRICE, TOTAL
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(is58 ? 18 : 28);
                            columns.RelativeColumn();
                            columns.ConstantColumn(is58 ? 44 : 64);
                            columns.ConstantColumn(is58 ? 44 : 64);
                        });

                        table.Header(header =>
                        {
                            header.Cell().AlignLeft().Text("QTY").FontSize(baseFontSize).Bold();
                            header.Cell().AlignLeft().Text("DESCRIPTION").FontSize(baseFontSize).Bold();
                            header.Cell().AlignRight().Text("UNIT PRICE").FontSize(baseFontSize).Bold();
                            header.Cell().AlignRight().Text("TOTAL").FontSize(baseFontSize).Bold();
                        });

                        foreach (var item in doc.Lines)
                        {
                            string desc = !string.IsNullOrWhiteSpace(item.Sku)
                                ? $"{item.Quantity:0.##}x {item.ItemName} ({item.Sku})"
                                : $"{item.Quantity:0.##}x {item.ItemName}";

                            table.Cell().AlignLeft().Text($"{item.Quantity:0.##}");
                            table.Cell().AlignLeft().Text(desc);
                            table.Cell().AlignRight().Text($"{cur} {item.UnitPrice:F2}");
                            table.Cell().AlignRight().Text($"{cur} {item.LineTotal:F2}");
                        }
                    });

                    // Divider
                    col.Item().PaddingTop(3).PaddingBottom(3).LineHorizontal(1).LineColor("#000000");

                    // 6. Financial Summary: SUBTOTAL, TAX (0%), BALANCE
                    col.Item().AlignRight().Column(totals =>
                    {
                        totals.Item().Row(r =>
                        {
                            r.AutoItem().Width(is58 ? 60 : 100).AlignRight().Text("SUBTOTAL").Bold();
                            r.AutoItem().Width(is58 ? 50 : 85).AlignRight().Text($"{cur} {doc.SubTotal:F2}").Bold();
                        });

                        totals.Item().Row(r =>
                        {
                            r.AutoItem().Width(is58 ? 60 : 100).AlignRight().Text("TAX (0%)").Bold();
                            r.AutoItem().Width(is58 ? 50 : 85).AlignRight().Text($"{cur} {doc.TaxAmount:F2}").Bold();
                        });

                        totals.Item().Row(r =>
                        {
                            r.AutoItem().Width(is58 ? 60 : 100).AlignRight().Text("BALANCE").Bold();
                            r.AutoItem().Width(is58 ? 50 : 85).AlignRight().Text($"{cur} {doc.GrandTotal:F2}").Bold();
                        });
                    });

                    // Solid Divider
                    col.Item().PaddingTop(3).PaddingBottom(4).LineHorizontal(1.5f).LineColor("#000000");

                    // 7. STORE POLICIES
                    col.Item().AlignCenter().Text("STORE POLICIES").FontSize(baseFontSize + 1).Bold();
                    col.Item().PaddingTop(2).Column(pol =>
                    {
                        var policies = new[]
                        {
                            "1. No returns or refunds are accepted, except for exchange within 3 days of purchase.",
                            "2. Items must be in original condition with tag.",
                            "3. For exchange, please bring the original receipt/invoice.",
                            "4. Thank you for your cooperation!"
                        };

                        foreach (var p in policies)
                        {
                            pol.Item().PaddingBottom(1).Text(p).FontSize(baseFontSize - 1);
                        }
                    });

                    // 8. Thank you for shopping with us!
                    col.Item().PaddingTop(6).AlignCenter().Text("Thank you for shopping with us!")
                        .FontSize(baseFontSize + 1).Bold().Italic();

                    // 9. Barcode of Invoice Number + Spaced Text
                    string barcodeVal = !string.IsNullOrWhiteSpace(doc.BarcodeOrQrValue)
                        ? doc.BarcodeOrQrValue
                        : doc.InvoiceNumber;

                    if (!string.IsNullOrWhiteSpace(barcodeVal))
                    {
                        col.Item().PaddingTop(5).AlignCenter().Column(bcCol =>
                        {
                            var pattern = BarcodeService.GetCode128Pattern(barcodeVal);
                            bcCol.Item().AlignCenter().Row(r =>
                            {
                                BarcodeService.RenderVectorBarcode(r.AutoItem(), pattern, is58 ? 20f : 26f, is58 ? 0.55f : 0.85f);
                            });

                            string spaced = "* " + string.Join(" ", barcodeVal.ToCharArray()) + " *";
                            bcCol.Item().PaddingTop(2).AlignCenter().Text(spaced).FontSize(baseFontSize - 0.5f).Bold();
                        });
                    }

                    // 10. In the last of slip: Powered by 7 Experts (Smaller text without outline border) + Website
                    col.Item().PaddingTop(6).AlignCenter().Column(pCol =>
                    {
                        pCol.Item().AlignCenter().Text("Powered by 7 Experts")
                            .FontSize(baseFontSize - 1.5f).Bold();
                        pCol.Item().PaddingTop(1).AlignCenter().Text("https://www.7expertspk.com/")
                            .FontSize(baseFontSize - 2.5f).FontColor("#555555");
                    });
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
                            string a4Cust = !string.IsNullOrWhiteSpace(doc.CustomerName) ? doc.CustomerName : "Walk-in Counter Sale";
                            if (!string.IsNullOrWhiteSpace(doc.CustomerPhone)) a4Cust += $" ({doc.CustomerPhone})";
                            bc.Item().Text(a4Cust).FontSize(11).Bold().FontColor("#0F172A");
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

