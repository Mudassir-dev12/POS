using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs;

public class OpenShiftDto
{
    public decimal OpeningCash { get; set; } = 0m;
    public string? Notes { get; set; }
}

public class CloseShiftDto
{
    public int ShiftId { get; set; }
    public decimal CountedCash { get; set; } = 0m;
    public string? Notes { get; set; }
}

public class CashMovementDto
{
    public int ShiftId { get; set; }
    public CashMovementType Type { get; set; } = CashMovementType.CashIn;
    public decimal Amount { get; set; } = 0m;
    public string Reason { get; set; } = string.Empty;
}

public class ShiftSummaryDto
{
    public int ShiftId { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public ShiftStatus Status { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningCash { get; set; }
    public int SalesCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalTax { get; set; }
    public decimal NetSales { get; set; }
    public int ReturnsCount { get; set; }
    public decimal TotalReturns { get; set; }
    public Dictionary<PaymentMethod, decimal> PaymentsByMethod { get; set; } = new();
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Difference { get; set; }
    public int HeldBillsCount { get; set; }
    public string? WarningMessage { get; set; }
}

public class ShiftSearchDto
{
    public ShiftStatus? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

