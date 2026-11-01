using ShopPOS.Core.Enums;
using ShopPOS.Core.Services;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Service contract for purchase order math, line calculations, invoice totals, and payment status resolution.
/// </summary>
public interface IPurchaseCalculator
{
    decimal Round(decimal value, int decimals = 2);

    LineCalculationResult CalculateLine(
        decimal quantity,
        decimal unitCost,
        decimal discountPercent,
        decimal taxPercent);

    PurchaseTotalsResult CalculateTotals(
        IEnumerable<LineCalculationResult> lines,
        decimal invoiceDiscount = 0m,
        decimal freightAmount = 0m);

    PurchasePaymentStatus DeterminePaymentStatus(
        decimal grandTotal,
        decimal paidAmount,
        DateTime? dueDate,
        PurchaseStatus status);
}

