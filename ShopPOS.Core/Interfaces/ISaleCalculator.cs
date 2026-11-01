namespace ShopPOS.Core.Interfaces;

public class SaleLineInput
{
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercent { get; set; }
}

public class SaleLineCalculationResult
{
    public decimal GrossAmount { get; set; }
    public decimal LineDiscountAmount { get; set; }
    public decimal NetAfterLineDiscount { get; set; }
    public decimal BillDiscountAllocated { get; set; }
    public decimal NetAfterAllDiscounts { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal NetOfTax { get; set; }
}

public class SaleTotalsResult
{
    public decimal SubTotal { get; set; }
    public decimal LineDiscountTotal { get; set; }
    public decimal BillDiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal PreRoundGrandTotal { get; set; }
    public decimal RoundOff { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal DueAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public List<SaleLineCalculationResult> Lines { get; set; } = new();
}

public interface ISaleCalculator
{
    decimal Round(decimal value, int decimals = 2);

    SaleTotalsResult CalculateSale(
        IEnumerable<SaleLineInput> lines,
        decimal billDiscountPercent = 0m,
        decimal billDiscountAmount = 0m,
        bool pricesIncludeTax = false,
        decimal cashRoundingStep = 0m,
        decimal paidAmount = 0m);

    SaleLineCalculationResult CalculateReturnLine(
        decimal quantity,
        decimal originalUnitPrice,
        decimal originalDiscountPercent,
        decimal originalDiscountAmount,
        decimal originalSoldQuantity,
        decimal taxPercent,
        bool pricesIncludeTax);
}

