namespace ShopPOS.Core.Enums;

/// <summary>
/// Supported barcode standards and symbologies.
/// </summary>
public enum BarcodeFormat
{
    /// <summary>
    /// European Article Number 13-digit standard barcode (GS1).
    /// </summary>
    Ean13,

    /// <summary>
    /// Universal Product Code 12-digit standard barcode (GS1 North America).
    /// </summary>
    UpcA,

    /// <summary>
    /// High-density alphanumeric barcode standard (Code 128 Auto).
    /// </summary>
    Code128
}
