using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Reports;

public class TaxRateSummaryDto
{
    public decimal TaxRatePercent { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal GrossTax { get; set; }
    public decimal ReturnsTax { get; set; }
    public decimal NetTax => GrossTax - ReturnsTax;
}

public class TaxReportDto
{
    public DateRangeDto DateRange { get; set; } = new();

    public List<TaxRateSummaryDto> OutputTaxRows { get; set; } = new();
    public List<TaxRateSummaryDto> InputTaxRows { get; set; } = new();

    public decimal TotalTaxableSales { get; set; }
    public decimal TotalOutputTax { get; set; }

    public decimal TotalTaxablePurchases { get; set; }
    public decimal TotalInputTax { get; set; }

    public decimal NetTaxPayable => TotalOutputTax - TotalInputTax;
}

