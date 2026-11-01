using ShopPOS.Core.Enums;

namespace ShopPOS.Core.Services;

public class LineCalculationResult
{
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal EffectiveNetUnitCost { get; set; }
}

public class PurchaseTotalsResult
{
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal GrandTotal { get; set; }
}

/// <summary>
/// Central pricing and rounding calculator for purchase orders and supplier invoices.
/// Enforces banker's-free 'round half away from zero' (MidpointRounding.AwayFromZero) to 2 decimals.
/// </summary>
public static class PurchaseCalculator
{
    public static decimal Round(decimal value, int decimals = 2)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    public static LineCalculationResult CalculateLine(
        decimal quantity,
        decimal unitCost,
        decimal discountPercent,
        decimal taxPercent)
    {
        decimal gross = Round(quantity * unitCost);

        decimal discountAmount = 0m;
        if (discountPercent > 0m)
        {
            discountAmount = Round(gross * (discountPercent / 100m));
        }

        decimal net = gross - discountAmount;
        if (net < 0m) net = 0m;

        decimal taxAmount = 0m;
        if (taxPercent > 0m)
        {
            taxAmount = Round(net * (taxPercent / 100m));
        }

        decimal lineTotal = net + taxAmount;
        decimal effectiveNetCost = quantity > 0m
            ? Math.Round(net / quantity, 4, MidpointRounding.AwayFromZero)
            : unitCost;

        return new LineCalculationResult
        {
            GrossAmount = gross,
            DiscountAmount = discountAmount,
            NetAmount = net,
            TaxAmount = taxAmount,
            LineTotal = lineTotal,
            EffectiveNetUnitCost = effectiveNetCost
        };
    }

    public static PurchaseTotalsResult CalculateTotals(
        IEnumerable<LineCalculationResult> lines,
        decimal invoiceDiscount = 0m,
        decimal freightAmount = 0m)
    {
        decimal subTotal = 0m;
        decimal taxTotal = 0m;

        foreach (var line in lines)
        {
            subTotal += line.NetAmount;
            taxTotal += line.TaxAmount;
        }

        subTotal = Round(subTotal);
        taxTotal = Round(taxTotal);
        decimal discount = Round(Math.Max(0m, invoiceDiscount));
        decimal freight = Round(Math.Max(0m, freightAmount));

        decimal grandTotal = Round(subTotal - discount + taxTotal + freight);
        if (grandTotal < 0m) grandTotal = 0m;

        return new PurchaseTotalsResult
        {
            SubTotal = subTotal,
            DiscountAmount = discount,
            TaxAmount = taxTotal,
            FreightAmount = freight,
            GrandTotal = grandTotal
        };
    }

    public static PurchasePaymentStatus DeterminePaymentStatus(
        decimal grandTotal,
        decimal paidAmount,
        DateTime? dueDate,
        PurchaseStatus status)
    {
        if (status == PurchaseStatus.Cancelled)
        {
            return PurchasePaymentStatus.Unpaid;
        }

        if (paidAmount >= grandTotal && grandTotal > 0)
        {
            return PurchasePaymentStatus.Paid;
        }

        bool isOverdue = dueDate.HasValue && dueDate.Value.Date < DateTime.Today;

        if (paidAmount > 0)
        {
            return isOverdue ? PurchasePaymentStatus.Overdue : PurchasePaymentStatus.Partial;
        }

        return isOverdue ? PurchasePaymentStatus.Overdue : PurchasePaymentStatus.Unpaid;
    }
}

public class PurchaseCalculatorService : ShopPOS.Core.Interfaces.IPurchaseCalculator
{
    public decimal Round(decimal value, int decimals = 2) =>
        PurchaseCalculator.Round(value, decimals);

    public LineCalculationResult CalculateLine(decimal quantity, decimal unitCost, decimal discountPercent, decimal taxPercent) =>
        PurchaseCalculator.CalculateLine(quantity, unitCost, discountPercent, taxPercent);

    public PurchaseTotalsResult CalculateTotals(IEnumerable<LineCalculationResult> lines, decimal invoiceDiscount = 0m, decimal freightAmount = 0m) =>
        PurchaseCalculator.CalculateTotals(lines, invoiceDiscount, freightAmount);

    public PurchasePaymentStatus DeterminePaymentStatus(decimal grandTotal, decimal paidAmount, DateTime? dueDate, PurchaseStatus status) =>
        PurchaseCalculator.DeterminePaymentStatus(grandTotal, paidAmount, dueDate, status);
}

