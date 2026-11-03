using System.Threading;
using System.Threading.Tasks;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Provides barcode validation, generation, collision checking, and label rendering.
/// </summary>
public interface IBarcodeService
{
    /// <summary>
    /// Calculates the standard Modulo-10 checksum digit for a 12-digit EAN code.
    /// </summary>
    int CalculateEan13CheckDigit(string twelveDigits);

    /// <summary>
    /// Validates whether a given barcode string conforms to the EAN-13 standard.
    /// </summary>
    bool ValidateEan13(string barcode);

    /// <summary>
    /// Calculates the standard Modulo-10 checksum digit for an 11-digit UPC-A code.
    /// </summary>
    int CalculateUpcACheckDigit(string elevenDigits);

    /// <summary>
    /// Validates whether a given barcode string conforms to the UPC-A standard.
    /// </summary>
    bool ValidateUpcA(string barcode);

    /// <summary>
    /// Validates whether a given barcode string is valid for Code 128 encoding.
    /// </summary>
    bool ValidateCode128(string barcode);

    /// <summary>
    /// Generates a valid 13-digit EAN-13 barcode with check digit.
    /// </summary>
    string GenerateEan13(string prefix = "20");

    /// <summary>
    /// Generates a valid 12-digit UPC-A barcode with check digit.
    /// </summary>
    string GenerateUpcA(string prefix = "0");

    /// <summary>
    /// Generates an alphanumeric Code 128 barcode.
    /// </summary>
    string GenerateCode128(string prefix = "PRD");

    /// <summary>
    /// Generates a unique barcode of the requested format that does not collide with any existing product or alias.
    /// </summary>
    Task<string> GenerateUniqueBarcodeAsync(BarcodeFormat format, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the bit pattern (true = black bar, false = space) for drawing vector barcode lines.
    /// </summary>
    bool[] GetBarcodePattern(string barcode, BarcodeFormat format);

    /// <summary>
    /// Generates a printable PDF document containing barcode labels.
    /// </summary>
    byte[] GenerateBarcodeLabelPdf(BarcodeLabelRequest request);
}
