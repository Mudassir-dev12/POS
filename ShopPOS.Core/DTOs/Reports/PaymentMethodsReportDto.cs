using System;
using System.Collections.Generic;
using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs.Reports;

public class PaymentMethodSummaryDto
{
    public PaymentMethod Method { get; set; }
    public string MethodName => Method.ToString();

    // Money In
    public decimal SalesCollected { get; set; }

    // Money Out
    public decimal RefundsPaid { get; set; }
    public decimal SupplierPaymentsPaid { get; set; }
    public decimal ExpensesPaid { get; set; }

    public decimal TotalMoneyIn => SalesCollected;
    public decimal TotalMoneyOut => RefundsPaid + SupplierPaymentsPaid + ExpensesPaid;
    public decimal NetCashFlow => TotalMoneyIn - TotalMoneyOut;
}

public class PaymentMethodsReportDto
{
    public DateRangeDto DateRange { get; set; } = new();
    public List<PaymentMethodSummaryDto> Rows { get; set; } = new();

    public decimal GrandTotalIn { get; set; }
    public decimal GrandTotalOut { get; set; }
    public decimal NetTotalFlow => GrandTotalIn - GrandTotalOut;
}

