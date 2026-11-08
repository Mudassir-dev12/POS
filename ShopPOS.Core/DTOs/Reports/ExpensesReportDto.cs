using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Reports;

public class ExpenseCategorySummaryDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int ExpenseCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PercentOfTotal { get; set; }
}

public class ExpenseDailySummaryDto
{
    public DateTime Date { get; set; }
    public int ExpenseCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CardAmount { get; set; }
    public decimal BankAmount { get; set; }
    public decimal MobileWalletAmount { get; set; }
    public decimal OtherAmount { get; set; }
}

public class ExpensesReportDto
{
    public DateRangeDto DateRange { get; set; } = new();
    public List<ExpenseCategorySummaryDto> ByCategoryRows { get; set; } = new();
    public List<ExpenseDailySummaryDto> ByDayRows { get; set; } = new();

    public int TotalExpenseCount { get; set; }
    public decimal TotalExpenseAmount { get; set; }
}

