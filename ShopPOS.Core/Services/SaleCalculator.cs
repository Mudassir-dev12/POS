using ShopPOS.Core.Interfaces;

namespace ShopPOS.Core.Services;

/// <summary>
/// Central calculator for all sales, returns, discounts, taxes, and cash rounding in ShopPOS.
/// Guarantees banker's-free 'round half away from zero' (MidpointRounding.AwayFromZero) to 2 decimals.
/// </summary>
public class SaleCalculator : ISaleCalculator
{
    public decimal Round(decimal value, int decimals = 2)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    public SaleTotalsResult CalculateSale(
        IEnumerable<SaleLineInput> lines,
        decimal billDiscountPercent = 0m,
        decimal billDiscountAmount = 0m,
        bool pricesIncludeTax = false,
        decimal cashRoundingStep = 0m,
        decimal paidAmount = 0m)
    {
        var lineList = lines.ToList();
        var result = new SaleTotalsResult();

        if (lineList.Count == 0)
        {
            return result;
        }

        // 1. First pass: compute line gross and line discounts
        var preliminaryLines = new List<(decimal Gross, decimal LineDiscount, decimal NetAfterLineDiscount, decimal TaxPercent)>();
        decimal totalNetAfterLineDiscounts = 0m;
        decimal totalGross = 0m;
        decimal totalLineDiscounts = 0m;

        foreach (var input in lineList)
        {
            decimal gross = Round(input.Quantity * input.UnitPrice);
            if (gross < 0m) gross = 0m;

            decimal lineDiscount = 0m;
            if (input.DiscountPercent > 0m)
            {
                lineDiscount = Round(gross * (Math.Min(100m, input.DiscountPercent) / 100m));
            }
            else if (input.DiscountAmount > 0m)
            {
                lineDiscount = Round(Math.Min(gross, Math.Max(0m, input.DiscountAmount)));
            }

            decimal net = Math.Max(0m, gross - lineDiscount);
            preliminaryLines.Add((gross, lineDiscount, net, input.TaxPercent));

            totalGross += gross;
            totalLineDiscounts += lineDiscount;
            totalNetAfterLineDiscounts += net;
        }

        // 2. Compute bill discount
        decimal totalBillDiscount = 0m;
        if (billDiscountPercent > 0m)
        {
            totalBillDiscount = Round(totalNetAfterLineDiscounts * (Math.Min(100m, billDiscountPercent) / 100m));
        }
        else if (billDiscountAmount > 0m)
        {
            totalBillDiscount = Round(Math.Min(totalNetAfterLineDiscounts, Math.Max(0m, billDiscountAmount)));
        }

        // 3. Second pass: distribute bill discount proportionally across lines & compute tax
        decimal allocatedBillDiscountSum = 0m;
        decimal totalTax = 0m;
        decimal preRoundGrandTotal = 0m;

        for (int i = 0; i < preliminaryLines.Count; i++)
        {
            var p = preliminaryLines[i];
            decimal allocatedBillDiscount = 0m;

            if (totalBillDiscount > 0m && totalNetAfterLineDiscounts > 0m)
            {
                if (i == preliminaryLines.Count - 1)
                {
                    // Last line absorbs any rounding remainder
                    allocatedBillDiscount = Math.Max(0m, totalBillDiscount - allocatedBillDiscountSum);
                    allocatedBillDiscount = Math.Min(allocatedBillDiscount, p.NetAfterLineDiscount);
                }
                else
                {
                    allocatedBillDiscount = Round(totalBillDiscount * (p.NetAfterLineDiscount / totalNetAfterLineDiscounts));
                    allocatedBillDiscount = Math.Min(allocatedBillDiscount, p.NetAfterLineDiscount);
                    allocatedBillDiscountSum += allocatedBillDiscount;
                }
            }

            decimal netAfterAllDiscounts = Math.Max(0m, p.NetAfterLineDiscount - allocatedBillDiscount);
            decimal taxRate = Math.Max(0m, p.TaxPercent) / 100m;
            decimal taxAmount;
            decimal lineTotal;
            decimal netOfTax;

            if (pricesIncludeTax)
            {
                // Price includes tax: extract tax from the discounted amount
                netOfTax = taxRate > 0m ? Round(netAfterAllDiscounts / (1m + taxRate)) : netAfterAllDiscounts;
                taxAmount = netAfterAllDiscounts - netOfTax;
                lineTotal = netAfterAllDiscounts;
            }
            else
            {
                // Price excludes tax: tax is added on top of the discounted amount
                netOfTax = netAfterAllDiscounts;
                taxAmount = taxRate > 0m ? Round(netAfterAllDiscounts * taxRate) : 0m;
                lineTotal = netAfterAllDiscounts + taxAmount;
            }

            result.Lines.Add(new SaleLineCalculationResult
            {
                GrossAmount = p.Gross,
                LineDiscountAmount = p.LineDiscount,
                NetAfterLineDiscount = p.NetAfterLineDiscount,
                BillDiscountAllocated = allocatedBillDiscount,
                NetAfterAllDiscounts = netAfterAllDiscounts,
                TaxAmount = taxAmount,
                LineTotal = lineTotal,
                NetOfTax = netOfTax
            });

            totalTax += taxAmount;
            preRoundGrandTotal += lineTotal;
        }

        result.SubTotal = Round(totalGross);
        result.LineDiscountTotal = Round(totalLineDiscounts);
        result.BillDiscountAmount = totalBillDiscount;
        result.TaxAmount = Round(totalTax);
        result.PreRoundGrandTotal = Round(preRoundGrandTotal);

        // 4. Cash rounding
        if (cashRoundingStep > 0m)
        {
            decimal rounded = Round(Math.Round(preRoundGrandTotal / cashRoundingStep, MidpointRounding.AwayFromZero) * cashRoundingStep);
            result.RoundOff = Round(rounded - preRoundGrandTotal);
            result.GrandTotal = rounded;
        }
        else
        {
            result.RoundOff = 0m;
            result.GrandTotal = Round(preRoundGrandTotal);
        }

        // 5. Due & Change
        if (paidAmount >= result.GrandTotal)
        {
            result.DueAmount = 0m;
            result.ChangeAmount = Round(paidAmount - result.GrandTotal);
        }
        else
        {
            result.DueAmount = Round(result.GrandTotal - paidAmount);
            result.ChangeAmount = 0m;
        }

        return result;
    }

    public SaleLineCalculationResult CalculateReturnLine(
        decimal quantity,
        decimal originalUnitPrice,
        decimal originalDiscountPercent,
        decimal originalDiscountAmount,
        decimal originalSoldQuantity,
        decimal taxPercent,
        bool pricesIncludeTax)
    {
        decimal gross = Round(quantity * originalUnitPrice);
        decimal effectiveDiscount = 0m;

        if (originalSoldQuantity > 0m && (originalDiscountAmount > 0m || originalDiscountPercent > 0m))
        {
            decimal unitDiscount = originalDiscountAmount / originalSoldQuantity;
            effectiveDiscount = Round(quantity * unitDiscount);
        }

        decimal netAfterDiscount = Math.Max(0m, gross - effectiveDiscount);
        decimal taxRate = Math.Max(0m, taxPercent) / 100m;
        decimal taxAmount;
        decimal lineTotal;
        decimal netOfTax;

        if (pricesIncludeTax)
        {
            netOfTax = taxRate > 0m ? Round(netAfterDiscount / (1m + taxRate)) : netAfterDiscount;
            taxAmount = netAfterDiscount - netOfTax;
            lineTotal = netAfterDiscount;
        }
        else
        {
            netOfTax = netAfterDiscount;
            taxAmount = taxRate > 0m ? Round(netAfterDiscount * taxRate) : 0m;
            lineTotal = netAfterDiscount + taxAmount;
        }

        return new SaleLineCalculationResult
        {
            GrossAmount = gross,
            LineDiscountAmount = effectiveDiscount,
            NetAfterLineDiscount = netAfterDiscount,
            BillDiscountAllocated = 0m,
            NetAfterAllDiscounts = netAfterDiscount,
            TaxAmount = taxAmount,
            LineTotal = lineTotal,
            NetOfTax = netOfTax
        };
    }
}

