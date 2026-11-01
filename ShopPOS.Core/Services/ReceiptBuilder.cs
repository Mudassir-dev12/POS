using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Services;

public class ReceiptBuilder : IReceiptBuilder
{
    public ReceiptDocument BuildFromSale(
        SaleDetailDto sale,
        ShopSetting settings,
        bool isDuplicate = false,
        int reprintCount = 0,
        decimal? customerPrevBalance = null,
        string? customerPhone = null,
        string? customerTaxNumber = null)
    {
        var doc = new ReceiptDocument
        {
            DocumentType = ReceiptDocumentType.SaleReceipt,
            DocumentTitle = !string.IsNullOrWhiteSpace(settings.TaxNumber) ? "TAX INVOICE" : "SALES RECEIPT",
            ShopName = settings.ShopName,
            ShopAddress = settings.Address,
            ShopPhone = settings.Phone,
            ShopTaxNumber = settings.TaxNumber,
            InvoiceNumber = sale.SaleNumber ?? $"INV-{sale.Id:D6}",
            DateTime = sale.SaleDate,
            CashierName = "Owner",
            CustomerName = sale.CustomerName,
            CustomerPhone = customerPhone,
            CustomerTaxNumber = customerTaxNumber,
            SubTotal = sale.SubTotal,
            LineDiscountTotal = sale.LineDiscountTotal,
            BillDiscountAmount = sale.BillDiscountAmount,
            TaxAmount = sale.TaxAmount,
            RoundOff = sale.RoundOff,
            GrandTotal = sale.GrandTotal,
            PaidAmount = sale.PaidAmount,
            ChangeAmount = sale.ChangeAmount,
            DueAmount = sale.DueAmount,
            FooterText = !string.IsNullOrWhiteSpace(settings.ReceiptFooter) ? settings.ReceiptFooter : "Thank you for shopping with us!",
            CurrencySymbol = settings.CurrencySymbol,
            IsDuplicateCopy = isDuplicate,
            ReprintCount = reprintCount,
            BarcodeOrQrValue = sale.SaleNumber,
            PaperWidthMm = settings.ReceiptWidthMm > 0 ? settings.ReceiptWidthMm : 80
        };

        // Line Items
        int idx = 1;
        foreach (var item in sale.Items)
        {
            doc.Lines.Add(new ReceiptLineItem
            {
                LineNo = idx++,
                ItemName = item.ProductName,
                Sku = item.Sku,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountPercent = item.DiscountPercent,
                DiscountAmount = item.DiscountAmount,
                TaxPercent = item.TaxPercent,
                LineTotal = item.LineTotal
            });
        }

        // Payments
        decimal tenderedTotal = 0m;
        foreach (var p in sale.Payments)
        {
            tenderedTotal += p.TenderedAmount ?? p.Amount;
            doc.Payments.Add(new ReceiptPaymentItem
            {
                Method = p.Method,
                Amount = p.Amount,
                TenderedAmount = p.TenderedAmount,
                Reference = p.Reference
            });
        }
        doc.TenderedAmount = tenderedTotal > 0m ? tenderedTotal : sale.PaidAmount;

        // Customer balances (when there is due or previous balance was provided)
        if (customerPrevBalance.HasValue)
        {
            doc.CustomerPreviousBalance = customerPrevBalance.Value;
            doc.CustomerNewBalance = customerPrevBalance.Value + sale.DueAmount;
        }

        return doc;
    }

    public ReceiptDocument BuildFromSaleReturn(
        SaleReturnDetailDto returnDetail,
        ShopSetting settings,
        bool isDuplicate = false)
    {
        var doc = new ReceiptDocument
        {
            DocumentType = ReceiptDocumentType.SaleReturnSlip,
            DocumentTitle = "SALE RETURN SLIP",
            ShopName = settings.ShopName,
            ShopAddress = settings.Address,
            ShopPhone = settings.Phone,
            ShopTaxNumber = settings.TaxNumber,
            InvoiceNumber = returnDetail.ReturnNumber,
            DateTime = returnDetail.ReturnDate,
            CashierName = "Owner",
            CustomerName = returnDetail.CustomerName,
            SubTotal = returnDetail.SubTotal,
            TaxAmount = returnDetail.TaxAmount,
            GrandTotal = returnDetail.TotalAmount,
            PaidAmount = returnDetail.RefundedAmount,
            FooterText = !string.IsNullOrWhiteSpace(settings.ReceiptFooter) ? settings.ReceiptFooter : "Refund processed successfully.",
            CurrencySymbol = settings.CurrencySymbol,
            IsDuplicateCopy = isDuplicate,
            BarcodeOrQrValue = returnDetail.ReturnNumber,
            PaperWidthMm = settings.ReceiptWidthMm > 0 ? settings.ReceiptWidthMm : 80
        };

        int idx = 1;
        foreach (var item in returnDetail.Items)
        {
            doc.Lines.Add(new ReceiptLineItem
            {
                LineNo = idx++,
                ItemName = item.ProductName,
                Sku = item.Sku,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TaxPercent = item.TaxPercent,
                LineTotal = item.LineTotal,
                IsReturn = true
            });
        }

        doc.Payments.Add(new ReceiptPaymentItem
        {
            Method = returnDetail.RefundMethod,
            Amount = returnDetail.RefundedAmount
        });

        return doc;
    }

    public ReceiptDocument BuildFromShift(
        ShiftSummaryDto shift,
        ShopSetting settings,
        bool isZReport = false)
    {
        var doc = new ReceiptDocument
        {
            DocumentType = ReceiptDocumentType.ShiftReport,
            DocumentTitle = isZReport ? "SHIFT Z REPORT (CLOSING)" : "SHIFT X REPORT (INTERIM)",
            ShopName = settings.ShopName,
            ShopAddress = settings.Address,
            ShopPhone = settings.Phone,
            ShopTaxNumber = settings.TaxNumber,
            InvoiceNumber = shift.ShiftNumber,
            DateTime = DateTime.Now,
            CashierName = "Owner",
            GrandTotal = shift.NetSales,
            FooterText = isZReport ? "Shift closed and reconciled." : "Interim reading - shift still open.",
            CurrencySymbol = settings.CurrencySymbol,
            PaperWidthMm = settings.ReceiptWidthMm > 0 ? settings.ReceiptWidthMm : 80,
            ShiftData = new ShiftSummaryReportData
            {
                ShiftNumber = shift.ShiftNumber,
                Status = shift.Status,
                OpenedAt = shift.OpenedAt,
                ClosedAt = shift.ClosedAt,
                OpeningCash = shift.OpeningCash,
                ExpectedCash = shift.ExpectedCash,
                CountedCash = shift.CountedCash,
                Difference = shift.Difference,
                SalesCount = shift.SalesCount,
                GrossSales = shift.GrossSales,
                NetSales = shift.NetSales,
                ReturnsCount = shift.ReturnsCount,
                TotalReturns = shift.TotalReturns,
                CashIn = shift.CashIn,
                CashOut = shift.CashOut,
                PaymentsByMethod = shift.PaymentsByMethod,
                IsZReport = isZReport
            }
        };

        return doc;
    }

    public ReceiptDocument BuildTestReceipt(ShopSetting settings)
    {
        var doc = new ReceiptDocument
        {
            DocumentType = ReceiptDocumentType.TestReceipt,
            DocumentTitle = "PRINTER TEST SLIP",
            ShopName = settings.ShopName,
            ShopAddress = settings.Address ?? "123 Main Street",
            ShopPhone = settings.Phone ?? "555-0199",
            ShopTaxNumber = settings.TaxNumber ?? "TAX-998877",
            InvoiceNumber = "TEST-0001",
            DateTime = DateTime.Now,
            CashierName = "Owner",
            CustomerName = "Valued Customer",
            SubTotal = 12.00m,
            TaxAmount = 1.20m,
            GrandTotal = 13.20m,
            PaidAmount = 20.00m,
            TenderedAmount = 20.00m,
            ChangeAmount = 6.80m,
            DueAmount = 0m,
            FooterText = "Test Print OK - Hardware Ready!",
            CurrencySymbol = settings.CurrencySymbol,
            BarcodeOrQrValue = "TEST-0001",
            PaperWidthMm = settings.ReceiptWidthMm > 0 ? settings.ReceiptWidthMm : 80
        };

        doc.Lines.Add(new ReceiptLineItem
        {
            LineNo = 1,
            ItemName = "Hardware Test Item A",
            Sku = "TEST-A",
            Quantity = 1m,
            UnitPrice = 5.00m,
            LineTotal = 5.00m
        });

        doc.Lines.Add(new ReceiptLineItem
        {
            LineNo = 2,
            ItemName = "Hardware Test Item B",
            Sku = "TEST-B",
            Quantity = 2m,
            UnitPrice = 3.50m,
            LineTotal = 7.00m
        });

        doc.Payments.Add(new ReceiptPaymentItem
        {
            Method = PaymentMethod.Cash,
            Amount = 13.20m,
            TenderedAmount = 20.00m
        });

        return doc;
    }
}

