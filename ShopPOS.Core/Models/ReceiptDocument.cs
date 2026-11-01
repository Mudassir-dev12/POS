using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Models;

public enum ReceiptDocumentType
{
    SaleReceipt = 0,
    SaleReturnSlip = 1,
    ShiftReport = 2,
    TestReceipt = 3
}

public class ReceiptLineItem
{
    public int LineNo { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsReturn { get; set; }
}

public class ReceiptPaymentItem
{
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public decimal Amount { get; set; }
    public decimal? TenderedAmount { get; set; }
    public string? Reference { get; set; }
}

/// <summary>
/// A printer-independent representation of a sales receipt, return slip, or shift report.
/// </summary>
public class ReceiptDocument
{
    public ReceiptDocumentType DocumentType { get; set; } = ReceiptDocumentType.SaleReceipt;
    public string DocumentTitle { get; set; } = "SALES RECEIPT";

    // Shop info
    public string ShopName { get; set; } = "Shop POS";
    public string? ShopAddress { get; set; }
    public string? ShopPhone { get; set; }
    public string? ShopTaxNumber { get; set; }

    // Transaction info
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime DateTime { get; set; } = DateTime.Now;
    public string CashierName { get; set; } = "Owner";

    // Customer info
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerTaxNumber { get; set; }

    // Items
    public List<ReceiptLineItem> Lines { get; set; } = new();

    // Financial totals
    public decimal SubTotal { get; set; }
    public decimal LineDiscountTotal { get; set; }
    public decimal BillDiscountAmount { get; set; }
    public decimal TotalDiscount => LineDiscountTotal + BillDiscountAmount;
    public decimal TaxAmount { get; set; }
    public decimal RoundOff { get; set; }
    public decimal GrandTotal { get; set; }

    // Payments & Settlement
    public List<ReceiptPaymentItem> Payments { get; set; } = new();
    public decimal PaidAmount { get; set; }
    public decimal TenderedAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public decimal DueAmount { get; set; }

    // Customer balances (when there is due or existing credit/debt)
    public decimal? CustomerPreviousBalance { get; set; }
    public decimal? CustomerNewBalance { get; set; }

    // Display & Meta
    public string FooterText { get; set; } = "Thank you for your business!";
    public string CurrencySymbol { get; set; } = "$";
    public bool IsDuplicateCopy { get; set; }
    public int ReprintCount { get; set; }
    public string? BarcodeOrQrValue { get; set; }

    // Paper width (58, 80, or 210 for A4)
    public int PaperWidthMm { get; set; } = 80;

    // Shift specific (for shift reports)
    public ShiftSummaryReportData? ShiftData { get; set; }
}

public class ShiftSummaryReportData
{
    public string ShiftNumber { get; set; } = string.Empty;
    public ShiftStatus Status { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Difference { get; set; }
    public int SalesCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal NetSales { get; set; }
    public int ReturnsCount { get; set; }
    public decimal TotalReturns { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public Dictionary<PaymentMethod, decimal> PaymentsByMethod { get; set; } = new();
    public bool IsZReport { get; set; }
}

