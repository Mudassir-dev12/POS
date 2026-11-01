using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs;

public class SaleLineItemDto
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; } = 0m;
    public decimal DiscountPercent { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal TaxPercent { get; set; } = 0m;
    public string? Note { get; set; }
}

public class SalePaymentDto
{
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public decimal Amount { get; set; } = 0m;
    public decimal? TenderedAmount { get; set; }
    public string? Reference { get; set; }
}

public class SaveSaleDto
{
    public int? Id { get; set; }
    public int? CustomerId { get; set; }
    public int? PriceLevelId { get; set; }
    public string? Notes { get; set; }
    public List<SaleLineItemDto> Items { get; set; } = new();
    public decimal BillDiscountPercent { get; set; } = 0m;
    public decimal BillDiscountAmount { get; set; } = 0m;
}

public class CompleteSaleDto
{
    public int? HeldBillId { get; set; }
    public int? CustomerId { get; set; }
    public int? PriceLevelId { get; set; }
    public string? Notes { get; set; }
    public List<SaleLineItemDto> Items { get; set; } = new();
    public decimal BillDiscountPercent { get; set; } = 0m;
    public decimal BillDiscountAmount { get; set; } = 0m;
    public List<SalePaymentDto> Payments { get; set; } = new();
}

public class SaleItemDetailDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal CostPriceSnapshot { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal LineTotal { get; set; }
    public string? Note { get; set; }
    public int LineNo { get; set; }
}

public class SalePaymentDetailDto
{
    public int Id { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public decimal? TenderedAmount { get; set; }
    public string? Reference { get; set; }
    public DateTime PaidAt { get; set; }
}

public class SaleDetailDto
{
    public int Id { get; set; }
    public string? SaleNumber { get; set; }
    public string? HoldNumber { get; set; }
    public DateTime SaleDate { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? PriceLevelId { get; set; }
    public string? PriceLevelName { get; set; }
    public SaleStatus Status { get; set; }
    public decimal SubTotal { get; set; }
    public decimal LineDiscountTotal { get; set; }
    public decimal BillDiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal RoundOff { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public decimal DueAmount { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }
    public int? ShiftId { get; set; }
    public string? ShiftNumber { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public bool IsOlderThan7Days { get; set; }

    public List<SaleItemDetailDto> Items { get; set; } = new();
    public List<SalePaymentDetailDto> Payments { get; set; } = new();
}

public class SaleSummaryDto
{
    public int Id { get; set; }
    public string? SaleNumber { get; set; }
    public string? HoldNumber { get; set; }
    public DateTime SaleDate { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public SaleStatus Status { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DueAmount { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Profit => GrandTotal - TotalCost;
    public string? ShiftNumber { get; set; }
}

public class SaleSearchDto
{
    public string? SearchTerm { get; set; }
    public int? CustomerId { get; set; }
    public int? ShiftId { get; set; }
    public SaleStatus? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class TodaySalesSummaryDto
{
    public int SalesCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalTax { get; set; }
    public decimal NetSales { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalReturnsCost { get; set; }
    public decimal Profit { get; set; }
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal BankSales { get; set; }
    public decimal OtherSales { get; set; }
}

