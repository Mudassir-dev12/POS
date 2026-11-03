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
        int reprintCount = 0)
    {
        var doc = new ReceiptDocument
        {
            DocumentType = ReceiptDocumentType.SaleReceipt,
            DocumentTitle = !string.IsNullOrWhiteSpace(settings.TaxNumber) ? "TAX INVOICE" : "SALES RECEIPT",
            ShopName = settings.ShopName,
            ShopAddress = settings.Address,
            ShopPhone = settings.Phone,
            ShopTaxNumber = settings.TaxNumber,
            ReceiptNumber = sale.SaleNumber ?? $"INV-{sale.Id:D6}",
            IssuedAt = sale.SaleDate,
            CashierName = "Owner",
            SubTotal = sale.SubTotal,
            LineDiscountTotal = sale.LineDiscountTotal,
            BillDiscountAmount = sale.BillDiscountAmount,
            TaxAmount = sale.TaxAmount,
            RoundOff = sale.RoundOff,
            GrandTotal = sale.GrandTotal,
            PaidAmount = sale.PaidAmount,
            ChangeAmount = sale.ChangeAmount,
            DueAmount = sale.DueAmount,
            FooterMessage = !string.IsNullOrWhiteSpace(settings.ReceiptFooter) ? settings.ReceiptFooter : "Thank you for your business!",
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
                ProductName = item.ProductName,
                Sku = item.Sku,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountAmount = item.DiscountAmount,
                TaxPercent = item.TaxPercent,
                LineTotal = item.LineTotal
            });
        }

        // Payments
        foreach (var p in sale.Payments)
        {
            doc.Payments.Add(new ReceiptPaymentItem
            {
                MethodName = p.Method.ToString(),
                Amount = p.Amount,
                TenderedAmount = p.TenderedAmount,
                Reference = p.Reference
            });
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
            DocumentType = ReceiptDocumentType.SaleReturn,
            DocumentTitle = "SALE RETURN SLIP",
            ShopName = settings.ShopName,
            ShopAddress = settings.Address,
            ShopPhone = settings.Phone,
            ShopTaxNumber = settings.TaxNumber,
            ReceiptNumber = returnDetail.ReturnNumber,
            IssuedAt = returnDetail.ReturnDate,
            CashierName = "Owner",
            SubTotal = returnDetail.SubTotal,
            TaxAmount = returnDetail.TaxAmount,
            GrandTotal = returnDetail.TotalAmount,
            PaidAmount = returnDetail.RefundedAmount,
            FooterMessage = !string.IsNullOrWhiteSpace(settings.ReceiptFooter) ? settings.ReceiptFooter : "Refund processed successfully.",
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
                ProductName = item.ProductName,
                Sku = item.Sku,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TaxPercent = item.TaxPercent,
                LineTotal = item.LineTotal
            });
        }

        doc.Payments.Add(new ReceiptPaymentItem
        {
            MethodName = returnDetail.RefundMethod.ToString(),
            Amount = returnDetail.RefundedAmount
        });

        return doc;
    }

    public ReceiptDocument BuildTestReceipt(ShopSetting settings)
    {
        var doc = new ReceiptDocument
        {
            DocumentType = ReceiptDocumentType.TestSlip,
            DocumentTitle = "PRINTER TEST SLIP",
            ShopName = settings.ShopName,
            ShopAddress = settings.Address ?? "123 Main Street",
            ShopPhone = settings.Phone ?? "555-0199",
            ShopTaxNumber = settings.TaxNumber ?? "TAX-998877",
            ReceiptNumber = "TEST-0001",
            IssuedAt = DateTime.Now,
            CashierName = "Owner",
            SubTotal = 12.00m,
            TaxAmount = 1.20m,
            GrandTotal = 13.20m,
            PaidAmount = 20.00m,
            ChangeAmount = 6.80m,
            DueAmount = 0m,
            FooterMessage = "Test Print OK - Hardware Ready!",
            CurrencySymbol = settings.CurrencySymbol,
            BarcodeOrQrValue = "TEST-0001",
            PaperWidthMm = settings.ReceiptWidthMm > 0 ? settings.ReceiptWidthMm : 80
        };

        doc.Lines.Add(new ReceiptLineItem
        {
            LineNo = 1,
            ProductName = "Hardware Test Item A",
            Sku = "TEST-A",
            Quantity = 1m,
            UnitPrice = 5.00m,
            LineTotal = 5.00m
        });

        doc.Lines.Add(new ReceiptLineItem
        {
            LineNo = 2,
            ProductName = "Hardware Test Item B",
            Sku = "TEST-B",
            Quantity = 2m,
            UnitPrice = 3.50m,
            LineTotal = 7.00m
        });

        doc.Payments.Add(new ReceiptPaymentItem
        {
            MethodName = PaymentMethod.Cash.ToString(),
            Amount = 13.20m,
            TenderedAmount = 20.00m
        });

        return doc;
    }
}
