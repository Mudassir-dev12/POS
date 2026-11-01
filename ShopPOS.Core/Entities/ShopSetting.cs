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

    /// <summary>
    /// Gets or sets the thermal receipt printer paper width in mm (58 or 80).
    /// </summary>
    public int ReceiptWidthMm { get; set; } = 80;

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
}

