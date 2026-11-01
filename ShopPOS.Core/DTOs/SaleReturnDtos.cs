using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs;

public class CreateSaleReturnItemDto
{
    public int? SaleItemId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public bool ReturnToStock { get; set; } = true;
    public string? Note { get; set; }
    public string? Reason { get => Note; set => Note = value; }
}

public class CreateSaleReturnDto
{
    public int? SaleId { get; set; }
    public int? CustomerId { get; set; }
    public int? ShiftId { get; set; }
    public PaymentMethod RefundMethod { get; set; } = PaymentMethod.Cash;
    public string? Reason { get; set; }
    public List<CreateSaleReturnItemDto> Items { get; set; } = new();
}

public class ReturnableLineDto
{
    public int SaleItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal SoldQuantity { get; set; }
    public decimal AlreadyReturnedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal OriginalUnitPrice { get; set; }
    public decimal OriginalDiscountPercent { get; set; }
    public decimal OriginalDiscountAmount { get; set; }
    public decimal OriginalTaxPercent { get; set; }
    public decimal LineTotal { get; set; }

    public decimal AvailableReturnQuantity => RemainingQuantity;
    public decimal UnitPrice => OriginalUnitPrice;
    public decimal TaxPercent => OriginalTaxPercent;
    public decimal EffectiveUnitPrice => SoldQuantity > 0m ? Math.Round(LineTotal / SoldQuantity, 2, MidpointRounding.AwayFromZero) : OriginalUnitPrice;
}

public class SaleReturnItemDetailDto
{
    public int Id { get; set; }
    public int? SaleItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal CostPriceSnapshot { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal LineTotal { get; set; }
    public bool ReturnToStock { get; set; }
}

public class SaleReturnDetailDto
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public int? SaleId { get; set; }
    public string? SaleNumber { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? ShiftId { get; set; }
    public string? ShiftNumber { get; set; }
    public SaleReturnStatus Status { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentMethod RefundMethod { get; set; }
    public decimal RefundedAmount { get; set; }
    public string? Reason { get; set; }
    public string? CancelReason { get; set; }

    public List<SaleReturnItemDetailDto> Items { get; set; } = new();
}

public class SaleReturnSearchDto
{
    public string? SearchTerm { get; set; }
    public int? SaleId { get; set; }
    public int? CustomerId { get; set; }
    public int? ShiftId { get; set; }
    public SaleReturnStatus? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

