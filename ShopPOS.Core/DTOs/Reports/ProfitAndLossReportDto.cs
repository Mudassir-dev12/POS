using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Reports;

public class ExpenseByCategoryRowDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal CurrentAmount { get; set; }
    public decimal PreviousAmount { get; set; }
    public decimal ChangeAmount => CurrentAmount - PreviousAmount;
}

public class ProfitAndLossReportDto
{
    public DateRangeDto CurrentPeriod { get; set; } = new();
    public DateRangeDto PreviousPeriod { get; set; } = new();

    // 1. Revenue
    public decimal CurrentGrossSales { get; set; }
    public decimal PreviousGrossSales { get; set; }

    public decimal CurrentSalesDiscounts { get; set; }
    public decimal PreviousSalesDiscounts { get; set; }

    public decimal CurrentSalesReturns { get; set; }
    public decimal PreviousSalesReturns { get; set; }

    public decimal CurrentNetSales => CurrentGrossSales - CurrentSalesDiscounts - CurrentSalesReturns;
    public decimal PreviousNetSales => PreviousGrossSales - PreviousSalesDiscounts - PreviousSalesReturns;

    // 2. Cost of Goods Sold
    public decimal CurrentCostOfSales { get; set; }
    public decimal PreviousCostOfSales { get; set; }

    public decimal CurrentCostOfReturnsRestocked { get; set; }
    public decimal PreviousCostOfReturnsRestocked { get; set; }

    public decimal CurrentCogs => CurrentCostOfSales - CurrentCostOfReturnsRestocked;
    public decimal PreviousCogs => PreviousCostOfSales - PreviousCostOfReturnsRestocked;

    // 3. Gross Profit
    public decimal CurrentGrossProfit => CurrentNetSales - CurrentCogs;
    public decimal PreviousGrossProfit => PreviousNetSales - PreviousCogs;
    public decimal CurrentGrossMarginPercent => CurrentNetSales != 0 ? Math.Round((CurrentGrossProfit / CurrentNetSales) * 100m, 2) : 0m;
    public decimal PreviousGrossMarginPercent => PreviousNetSales != 0 ? Math.Round((PreviousGrossProfit / PreviousNetSales) * 100m, 2) : 0m;

    // 4. Operating Expenses
    public List<ExpenseByCategoryRowDto> ExpensesByCategory { get; set; } = new();
    public decimal CurrentTotalExpenses { get; set; }
    public decimal PreviousTotalExpenses { get; set; }

    // 5. Stock Write-Offs (Damaged, Expired, Lost)
    public decimal CurrentStockWriteOffs { get; set; }
    public decimal PreviousStockWriteOffs { get; set; }

    // Positive "Found" stock adjustments shown separately (not added to profit)
    public decimal CurrentFoundStockValuation { get; set; }
    public decimal PreviousFoundStockValuation { get; set; }

    // 6. Net Profit
    public decimal CurrentNetProfit => CurrentGrossProfit - CurrentTotalExpenses - CurrentStockWriteOffs;
    public decimal PreviousNetProfit => PreviousGrossProfit - PreviousTotalExpenses - PreviousStockWriteOffs;
    public decimal CurrentNetMarginPercent => CurrentNetSales != 0 ? Math.Round((CurrentNetProfit / CurrentNetSales) * 100m, 2) : 0m;
    public decimal PreviousNetMarginPercent => PreviousNetSales != 0 ? Math.Round((PreviousNetProfit / PreviousNetSales) * 100m, 2) : 0m;
}

