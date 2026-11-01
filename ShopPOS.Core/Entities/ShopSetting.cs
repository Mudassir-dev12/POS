using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents store-wide configuration and receipt details.
/// </summary>
public class ShopSetting : BaseEntity
{
    /// <summary>
    /// Gets or sets the display name of the shop.
    /// </summary>
    public string ShopName { get; set; } = "Shop POS";

    /// <summary>
    /// Gets or sets the physical shop address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets the shop telephone number.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the tax / VAT registration number.
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Gets or sets the currency symbol (e.g. $, ₹, £, €).
    /// </summary>
    public string CurrencySymbol { get; set; } = "$";

    /// <summary>
    /// Gets or sets the custom text printed at the bottom of customer receipts.
    /// </summary>
    public string? ReceiptFooter { get; set; } = "Thank you for your business!";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? ReceiptFooterText { get => ReceiptFooter; set => ReceiptFooter = value; }

    /// <summary>
    /// Gets or sets the thermal receipt printer paper width in mm (58 or 80).
    /// </summary>
    public int ReceiptWidthMm { get; set; } = 80;

    /// <summary>
    /// Gets or sets the receipt printing output mode (PdfPreview, WindowsDriver, EscPosRaw). Default is PdfPreview.
    /// </summary>
    public ShopPOS.Core.Enums.PrinterMode PrinterMode { get; set; } = ShopPOS.Core.Enums.PrinterMode.PdfPreview;

    /// <summary>
    /// Gets or sets the default sales tax percentage.
    /// </summary>
    public decimal DefaultTaxPercent { get; set; } = 0m;

    /// <summary>
    /// Gets or sets the inventory costing method (WeightedAverage or LastCost). Default is WeightedAverage.
    /// </summary>
    public ShopPOS.Core.Enums.CostingMethod CostingMethod { get; set; } = ShopPOS.Core.Enums.CostingMethod.WeightedAverage;

    /// <summary>
    /// Gets or sets whether inventory levels are allowed to become negative. Default is false.
    /// </summary>
    public bool AllowNegativeStock { get; set; } = false;

    /// <summary>
    /// Gets or sets the default multiplier applied to ReorderLevel when calculating suggested restock quantities. Default is 2.
    /// </summary>
    public decimal DefaultReorderMultiplier { get; set; } = 2.0m;

    /// <summary>
    /// Gets or sets whether completing a sale requires an active open shift. Default is true.
    /// </summary>
    public bool RequireOpenShiftForSales { get; set; } = true;

    /// <summary>
    /// Gets or sets whether sales on credit (unpaid or partially paid) are permitted. Default is true.
    /// </summary>
    public bool AllowCreditSales { get; set; } = true;

    /// <summary>
    /// Gets or sets whether selling below the product's MinSalePrice is permitted. Default is false.
    /// </summary>
    public bool AllowSellingBelowMinPrice { get; set; } = false;

    /// <summary>
    /// Gets or sets whether catalog selling prices already include tax. Default is false.
    /// </summary>
    public bool PricesIncludeTax { get; set; } = false;

    /// <summary>
    /// Gets or sets the cash rounding denomination step (e.g. 0.05, 1, 5). 0 = rounding disabled. Default is 0.
    /// </summary>
    public decimal CashRoundingStep { get; set; } = 0m;

    /// <summary>
    /// Gets or sets whether returns can be processed without referencing the original sale receipt. Default is false.
    /// </summary>
    public bool AllowReturnWithoutReceipt { get; set; } = false;

    /// <summary>
    /// Gets or sets whether receipts are automatically printed upon sale completion. Default is true.
    /// </summary>
    public bool AutoPrintReceipt { get; set; } = true;

    /// <summary>
    /// Gets or sets the configured thermal receipt printer name.
    /// </summary>
    public string? ReceiptPrinterName { get; set; }

    /// <summary>
    /// Gets or sets whether the cash drawer kick pulse is sent on cash sales. Default is false.
    /// </summary>
    public bool OpenCashDrawerOnCashSale { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of receipt copies to print per transaction. Default is 1.
    /// </summary>
    public int ReceiptCopies { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum window in days for customer returns against a receipt. Null = unlimited.
    /// </summary>
    public int? SaleReturnWindowDays { get; set; }
}
