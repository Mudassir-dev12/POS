namespace ShopPOS.Core.Models;

public enum ReceiptDocumentType
{
    SaleReceipt = 0,
    SaleReturn = 1,
    TestSlip = 2
}

public class ReceiptLineItem
{
    public int LineNo { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ItemName { get => ProductName; set => ProductName = value; }
    public string? Sku { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxPercent { get; set; }
}

public class ReceiptPaymentItem
{
    public string MethodName { get; set; } = string.Empty;
    public string Method { get => MethodName; set => MethodName = value; }
    public decimal Amount { get; set; }
    public decimal? TenderedAmount { get; set; }
    public string? Reference { get; set; }
}

/// <summary>
/// A printer-independent representation of a sales receipt, return slip, or test slip.
/// </summary>
public class ReceiptDocument
{
    public ReceiptDocumentType DocumentType { get; set; } = ReceiptDocumentType.SaleReceipt;
    public string DocumentTitle { get; set; } = "TAX INVOICE";

    // Shop / Store Header
    public string ShopName { get; set; } = "Shop POS";
    public string? ShopAddress { get; set; }
    public string? ShopPhone { get; set; }
    public string? ShopTaxNumber { get; set; }

    // Receipt Metadata
    public string ReceiptNumber { get; set; } = string.Empty;
    public string InvoiceNumber { get => ReceiptNumber; set => ReceiptNumber = value; }
    public DateTime IssuedAt { get; set; } = DateTime.Now;
    public DateTime DateTime { get => IssuedAt; set => IssuedAt = value; }
    public string CashierName { get; set; } = "Owner";

    // Line items
    public List<ReceiptLineItem> Lines { get; set; } = new();

    // Financial breakdown
    public decimal SubTotal { get; set; }
    public decimal LineDiscountTotal { get; set; }
    public decimal BillDiscountAmount { get; set; }
    public decimal TotalDiscount => LineDiscountTotal + BillDiscountAmount;
    public decimal TaxAmount { get; set; }
    public decimal RoundOff { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public decimal DueAmount { get; set; }

    // Tender and Payments breakdown
    public List<ReceiptPaymentItem> Payments { get; set; } = new();

    // Footer & Branding
    public string? FooterMessage { get; set; } = "Thank you for your business!";
    public string? FooterText { get => FooterMessage; set => FooterMessage = value; }
    public bool IsDuplicateCopy { get; set; } = false;
    public int ReprintCount { get; set; } = 0;
    public string? BarcodeOrQrValue { get; set; }

    // Paper width (58 or 80 mm)
    public int PaperWidthMm { get; set; } = 80;
    public string CurrencySymbol { get; set; } = "Rs";
}
