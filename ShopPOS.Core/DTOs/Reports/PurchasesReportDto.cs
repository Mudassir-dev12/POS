using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Reports;

public class PurchaseSupplierRowDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int InvoiceCount { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal ReturnsTotal { get; set; }
    public decimal NetPurchases => GrandTotal - ReturnsTotal;
    public decimal PaidAmount { get; set; }
    public decimal CurrentBalance { get; set; }
}

public class PurchaseProductRowDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityPurchased { get; set; }
    public decimal TotalCost { get; set; }
    public decimal ReturnsQuantity { get; set; }
    public decimal NetQuantity => QuantityPurchased - ReturnsQuantity;
    public decimal NetSpend { get; set; }
}

public class PurchaseDailyRowDto
{
    public DateTime Date { get; set; }
    public int InvoiceCount { get; set; }
    public decimal PurchasesTotal { get; set; }
    public decimal ReturnsTotal { get; set; }
    public decimal NetPurchases => PurchasesTotal - ReturnsTotal;
    public decimal PaymentsMade { get; set; }
}

public class SupplierPayablesAgingRowDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal TotalBalance { get; set; }
    public decimal CurrentBucket { get; set; } // 0-30 days
    public decimal Bucket31To60 { get; set; }
    public decimal Bucket61To90 { get; set; }
    public decimal BucketOver90 { get; set; }
}

public class PurchasesReportDto
{
    public DateRangeDto DateRange { get; set; } = new();
    public List<PurchaseSupplierRowDto> BySupplierRows { get; set; } = new();
    public List<PurchaseProductRowDto> ByProductRows { get; set; } = new();
    public List<PurchaseDailyRowDto> ByDayRows { get; set; } = new();
    public List<SupplierPayablesAgingRowDto> AgingRows { get; set; } = new();

    public decimal TotalNetPurchases { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalPayablesBalance { get; set; }
}

