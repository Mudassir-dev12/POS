using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Reports;

public enum DataQualityIssueType
{
    MissingBarcode,
    DuplicateBarcode,
    InvalidCheckDigit,
    ZeroCostPrice,
    PriceBelowCost,
    NegativeStock
}

public class DataQualityIssueDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal CurrentStock { get; set; }
    public DataQualityIssueType IssueType { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class BarcodeDataQualityReportDto
{
    public List<DataQualityIssueDto> Issues { get; set; } = new();

    public int MissingBarcodeCount { get; set; }
    public int DuplicateBarcodeCount { get; set; }
    public int InvalidCheckDigitCount { get; set; }
    public int ZeroCostPriceCount { get; set; }
    public int PriceBelowCostCount { get; set; }
    public int NegativeStockCount { get; set; }
    public int TotalIssueCount => Issues.Count;
}

