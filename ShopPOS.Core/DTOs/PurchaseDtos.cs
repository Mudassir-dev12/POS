using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs;

public class PurchaseSearchQuery
{
    public string? SearchTerm { get; set; }
    public int? SupplierId { get; set; }
    public PurchaseStatus? Status { get; set; }
    public PurchasePaymentStatus? PaymentStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class PurchaseListDto
{
    public int Id { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierInvoiceNo { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance => Math.Max(0m, GrandTotal - PaidAmount);
    public PurchasePaymentStatus PaymentStatus { get; set; }
    public PurchaseStatus Status { get; set; }
    public DateTime? DueDate { get; set; }
    public int ItemCount { get; set; }
}

public class PurchaseItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal LineTotal { get; set; }
    public string? Note { get; set; }
}

public class PurchaseDetailDto
{
    public int Id { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierInvoiceNo { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? DueDate { get; set; }
    public PurchaseStatus Status { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance => Math.Max(0m, GrandTotal - PaidAmount);
    public PurchasePaymentStatus PaymentStatus { get; set; }

    public string? Notes { get; set; }

    public DateTime? PostedAt { get; set; }
    public string? PostedByName { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancelledByName { get; set; }
    public string? CancelReason { get; set; }

    public List<PurchaseItemDto> Items { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class CreatePurchaseItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TaxPercent { get; set; }
    public string? Note { get; set; }
}

public class CreatePurchaseDto
{
    public int SupplierId { get; set; }
    public string? SupplierInvoiceNo { get; set; }
    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public DateTime? DueDate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FreightAmount { get; set; }
    public string? Notes { get; set; }

    public List<CreatePurchaseItemDto> Items { get; set; } = new();
}

public class UpdatePurchaseDto : CreatePurchaseDto
{
    public int Id { get; set; }
}

public class SupplierPaymentDto
{
    public int Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int? PurchaseId { get; set; }
    public string? PurchaseNumber { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledByName { get; set; }
}

public class CreateSupplierPaymentDto
{
    public int SupplierId { get; set; }
    public int? PurchaseId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public class PurchaseReturnItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public string? Note { get; set; }
}

public class PurchaseReturnDto
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int? PurchaseId { get; set; }
    public string? PurchaseNumber { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public PurchaseReturnStatus Status { get; set; }
    public List<PurchaseReturnItemDto> Items { get; set; } = new();
}

public class CreatePurchaseReturnItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Note { get; set; }
}

public class CreatePurchaseReturnDto
{
    public int SupplierId { get; set; }
    public int? PurchaseId { get; set; }
    public DateTime ReturnDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }
    public List<CreatePurchaseReturnItemDto> Items { get; set; } = new();
}

public class SupplierLedgerEntryDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public class SupplierLedgerReportDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<SupplierLedgerEntryDto> Entries { get; set; } = new();
}

public class ShopSettingDto
{
    public string ShopName { get; set; } = "Shop POS";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? TaxNumber { get; set; }
    public string CurrencySymbol { get; set; } = "$";
}
