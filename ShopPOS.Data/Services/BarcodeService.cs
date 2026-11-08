using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Services;

/// <summary>
/// Implements GS1 standard barcode algorithms (EAN-13, UPC-A, Code 128),
/// collision-free generation, vector rendering, and printable PDF label sheets.
/// </summary>
public class BarcodeService : IBarcodeService
{
    private readonly AppDbContext _dbContext;

    static BarcodeService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public BarcodeService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // =========================================================================
    // 1. CHECKSUM & VALIDATION ALGORITHMS
    // =========================================================================

    public int CalculateEan13CheckDigit(string twelveDigits)
    {
        if (string.IsNullOrWhiteSpace(twelveDigits) || twelveDigits.Length < 12 || !twelveDigits.All(char.IsDigit))
        {
            throw new ArgumentException("EAN-13 check digit requires at least 12 numeric digits.", nameof(twelveDigits));
        }

        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int digit = twelveDigits[i] - '0';
            // Odd indices in 0-based are 2nd, 4th, 6th... (weight 3), even indices are 1st, 3rd, 5th... (weight 1)
            sum += (i % 2 == 0) ? digit : digit * 3;
        }

        int mod = sum % 10;
        return (mod == 0) ? 0 : 10 - mod;
    }

    public bool ValidateEan13(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || barcode.Length != 13 || !barcode.All(char.IsDigit))
        {
            return false;
        }

        int expectedCheck = CalculateEan13CheckDigit(barcode.Substring(0, 12));
        int actualCheck = barcode[12] - '0';
        return expectedCheck == actualCheck;
    }

    public int CalculateUpcACheckDigit(string elevenDigits)
    {
        if (string.IsNullOrWhiteSpace(elevenDigits) || elevenDigits.Length < 11 || !elevenDigits.All(char.IsDigit))
        {
            throw new ArgumentException("UPC-A check digit requires at least 11 numeric digits.", nameof(elevenDigits));
        }

        int sum = 0;
        for (int i = 0; i < 11; i++)
        {
            int digit = elevenDigits[i] - '0';
            // In UPC-A: 1st, 3rd, 5th... weight 3, 2nd, 4th, 6th... weight 1
            sum += (i % 2 == 0) ? digit * 3 : digit;
        }

        int mod = sum % 10;
        return (mod == 0) ? 0 : 10 - mod;
    }

    public bool ValidateUpcA(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || barcode.Length != 12 || !barcode.All(char.IsDigit))
        {
            return false;
        }

        int expectedCheck = CalculateUpcACheckDigit(barcode.Substring(0, 11));
        int actualCheck = barcode[11] - '0';
        return expectedCheck == actualCheck;
    }

    public bool ValidateCode128(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return false;
        // Code 128 accepts ASCII 32 through 126
        return barcode.All(c => c >= 32 && c <= 126);
    }

    // =========================================================================
    // 2. BARCODE GENERATION
    // =========================================================================

    public string GenerateEan13(string prefix = "20")
    {
        // GS1 standard: 20-29 reserved for internal retailer usage
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "20";
        if (prefix.Length > 11) prefix = prefix.Substring(0, 11);

        int randomLength = 12 - prefix.Length;
        string randomDigits = GenerateRandomDigits(randomLength);
        string first12 = prefix + randomDigits;
        int checkDigit = CalculateEan13CheckDigit(first12);
        return first12 + checkDigit.ToString();
    }

    public string GenerateUpcA(string prefix = "0")
    {
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "0";
        if (prefix.Length > 10) prefix = prefix.Substring(0, 10);

        int randomLength = 11 - prefix.Length;
        string randomDigits = GenerateRandomDigits(randomLength);
        string first11 = prefix + randomDigits;
        int checkDigit = CalculateUpcACheckDigit(first11);
        return first11 + checkDigit.ToString();
    }

    public string GenerateCode128(string prefix = "PRD")
    {
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "PRD";
        string randomAlphaNum = GenerateRandomAlphaNumeric(6);
        return $"{prefix}-{randomAlphaNum}".ToUpperInvariant();
    }

    public async Task<string> GenerateUniqueBarcodeAsync(BarcodeFormat format, CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 100;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            string candidate = format switch
            {
                BarcodeFormat.Ean13 => GenerateEan13("20"),
                BarcodeFormat.UpcA => GenerateUpcA("0"),
                BarcodeFormat.Code128 => GenerateCode128("PRD"),
                _ => GenerateEan13("20")
            };

            bool exists = await _dbContext.Products
                .AnyAsync(p => !p.IsDeleted && p.Barcode == candidate, cancellationToken);

            if (!exists)
            {
                exists = await _dbContext.ProductBarcodes
                    .AnyAsync(pb => pb.Barcode == candidate, cancellationToken);
            }

            if (!exists)
            {
                return candidate;
            }
        }

        // Fallback timestamp-based code
        string fallback = "20" + DateTime.UtcNow.ToString("yyMMddHHmmss").Substring(0, 10);
        return fallback + CalculateEan13CheckDigit(fallback);
    }

    private static string GenerateRandomDigits(int length)
    {
        var sb = new StringBuilder(length);
        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        for (int i = 0; i < length; i++)
        {
            sb.Append((buffer[i] % 10).ToString());
        }
        return sb.ToString();
    }

    private static string GenerateRandomAlphaNumeric(int length)
    {
        const string chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // exclude easily confused chars 0, 1, I, O
        var sb = new StringBuilder(length);
        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        for (int i = 0; i < length; i++)
        {
            sb.Append(chars[buffer[i] % chars.Length]);
        }
        return sb.ToString();
    }

    // =========================================================================
    // 3. VECTOR BARCODE BIT PATTERN GENERATION
    // =========================================================================

    public bool[] GetBarcodePattern(string barcode, BarcodeFormat format)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Array.Empty<bool>();
        }

        return format switch
        {
            BarcodeFormat.Ean13 => GetEan13Pattern(barcode),
            BarcodeFormat.UpcA => GetEan13Pattern("0" + barcode), // UPC-A is a subset of EAN-13 with leading 0
            BarcodeFormat.Code128 => GetCode128Pattern(barcode),
            _ => GetCode128Pattern(barcode)
        };
    }

    #region EAN-13 Symbology

    private static readonly string[] EanLCode =
    {
        "0001101", "0011001", "0010011", "0111101", "0100011",
        "0110001", "0101111", "0111011", "0110111", "0001011"
    };

    private static readonly string[] EanGCode =
    {
        "0100111", "0110011", "0011011", "0100001", "0011101",
        "0111001", "0000101", "0010001", "0001001", "0010111"
    };

    private static readonly string[] EanRCode =
    {
        "1110010", "1100110", "1101100", "1000010", "1011100",
        "1001110", "1010000", "1000100", "1001000", "1110100"
    };

    private static readonly string[] EanParity =
    {
        "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG",
        "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"
    };

    private bool[] GetEan13Pattern(string barcode)
    {
        if (!ValidateEan13(barcode))
        {
            // If invalid checksum, compute it for visual rendering
            if (barcode.Length == 12 && barcode.All(char.IsDigit))
            {
                barcode += CalculateEan13CheckDigit(barcode).ToString();
            }
            else
            {
                return GetCode128Pattern(barcode); // Fallback to Code 128
            }
        }

        int firstDigit = barcode[0] - '0';
        string parity = EanParity[firstDigit];

        var bits = new List<bool>(113);

        // Quiet Zone (9 modules)
        for (int i = 0; i < 9; i++) bits.Add(false);

        // Start Guard: 101
        bits.Add(true); bits.Add(false); bits.Add(true);

        // Left 6 digits
        for (int i = 1; i <= 6; i++)
        {
            int digit = barcode[i] - '0';
            char p = parity[i - 1];
            string pattern = (p == 'L') ? EanLCode[digit] : EanGCode[digit];
            foreach (char c in pattern) bits.Add(c == '1');
        }

        // Center Guard: 01010
        bits.Add(false); bits.Add(true); bits.Add(false); bits.Add(true); bits.Add(false);

        // Right 6 digits (always R code)
        for (int i = 7; i <= 12; i++)
        {
            int digit = barcode[i] - '0';
            string pattern = EanRCode[digit];
            foreach (char c in pattern) bits.Add(c == '1');
        }

        // End Guard: 101
        bits.Add(true); bits.Add(false); bits.Add(true);

        // Quiet Zone (9 modules)
        for (int i = 0; i < 9; i++) bits.Add(false);

        return bits.ToArray();
    }

    #endregion

    #region Code 128 Symbology

    // Standard Code 128 Patterns (values 0 to 106)
    private static readonly string[] Code128Patterns =
    {
        "11011001100", "11001101100", "11001100110", "10010011000", "10010001100",
        "10001001100", "10011001000", "10011000100", "10001100100", "11001001000",
        "11001000100", "11000100100", "10110011100", "10011011100", "10011001110",
        "10111001100", "10011101100", "10011100110", "11001110010", "11001011100",
        "11001001110", "11011100100", "11001110100", "11101101110", "11101001100",
        "11100101100", "11100100110", "11101100100", "11100110100", "11100110010",
        "11011011000", "11011000110", "11000110110", "10100011000", "10001011000",
        "10001000110", "10110001000", "10001101000", "10001100010", "11010001000",
        "11000101000", "11000100010", "10110111000", "10110001110", "10001101110",
        "10111011000", "10111000110", "10001110110", "11101110110", "11010001110",
        "11000101110", "11011101000", "11011100010", "11011101110", "11101011000",
        "11101000110", "11100010110", "11101101000", "11101100010", "11100011010",
        "11101111010", "11001000010", "11110001010", "10100110000", "10100001100",
        "10010110000", "10010000110", "10000101100", "10000100110", "10110010000",
        "10110000100", "10011010000", "10011000010", "10000110100", "10000110010",
        "11000010010", "11001010000", "11110111010", "11000010100", "10001111010",
        "10100111100", "10010111100", "10010011110", "10111100100", "10011110100",
        "10011110010", "11110100100", "11110010100", "11110010010", "11011011110",
        "11011110110", "11110110110", "10101111000", "10100011110", "10001011110",
        "10111101000", "10111100010", "11110101000", "11110100010", "10111011110",
        "10111101110", "11101011110", "11110101110", "11010000100", "11010010000",
        "11010011100", "1100011101011" // 106 = Stop pattern
    };

    internal static bool[] GetCode128Pattern(string barcode)
    {
        var bits = new List<bool>();

        // Quiet Zone (10 modules)
        for (int i = 0; i < 10; i++) bits.Add(false);

        // Start B = index 104
        int checksum = 104;
        AppendCode128Symbol(bits, Code128Patterns[104]);

        int weight = 1;
        foreach (char c in barcode)
        {
            int val = c - 32; // Code 128 Set B ASCII map (space=0..~=94)
            if (val < 0 || val > 94) val = 0;
            checksum += val * weight;
            weight++;
            AppendCode128Symbol(bits, Code128Patterns[val]);
        }

        // Checksum symbol
        int checkVal = checksum % 103;
        AppendCode128Symbol(bits, Code128Patterns[checkVal]);

        // Stop Symbol (index 106, 13 bits)
        AppendCode128Symbol(bits, Code128Patterns[106]);

        // Quiet Zone (10 modules)
        for (int i = 0; i < 10; i++) bits.Add(false);

        return bits.ToArray();
    }

    private static void AppendCode128Symbol(List<bool> bits, string pattern)
    {
        foreach (char c in pattern)
        {
            bits.Add(c == '1');
        }
    }

    #endregion

    // =========================================================================
    // 4. PRINTABLE BARCODE LABELS (QUESTPDF)
    // =========================================================================

    public byte[] GenerateBarcodeLabelPdf(BarcodeLabelRequest request)
    {
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

        // Unfold items according to Quantity
        var labelList = new List<BarcodeLabelItem>();
        foreach (var item in request.Items)
        {
            int count = Math.Max(1, item.Quantity);
            for (int i = 0; i < count; i++)
            {
                labelList.Add(item);
            }
        }

        if (labelList.Count == 0)
        {
            labelList.Add(new BarcodeLabelItem
            {
                ProductName = "Sample Product",
                Sku = "P000001",
                Barcode = "2012345678903",
                FormattedPrice = "Rs 100.00",
                Format = BarcodeFormat.Ean13
            });
        }

        var document = Document.Create(container =>
        {
            if (request.Layout == BarcodeLabelLayout.A4Sheet30Up)
            {
                ComposeA4Sheet30Up(container, request, labelList);
            }
            else
            {
                ComposeThermalRoll(container, request, labelList);
            }
        });

        return document.GeneratePdf();
    }

    private void ComposeA4Sheet30Up(IDocumentContainer container, BarcodeLabelRequest request, List<BarcodeLabelItem> labels)
    {
        // A4 30-up standard labels (3 columns x 10 rows per sheet, ~70mm x 29.7mm per sticker)
        const int labelsPerPage = 30;
        int pageCount = (int)Math.Ceiling(labels.Count / (double)labelsPerPage);

        for (int p = 0; p < pageCount; p++)
        {
            var pageLabels = labels.Skip(p * labelsPerPage).Take(labelsPerPage).ToList();

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(7, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    for (int i = 0; i < 30; i++)
                    {
                        var labelItem = i < pageLabels.Count ? pageLabels[i] : null;

                        table.Cell().Height(27.5f, Unit.Millimetre).Padding(2).Border(0.5f).BorderColor("#E2E8F0").Element(cell =>
                        {
                            if (labelItem != null)
                            {
                                RenderSingleLabel(cell, request, labelItem, isThermal: false);
                            }
                        });
                    }
                });
            });
        }
    }

    private void ComposeThermalRoll(IDocumentContainer container, BarcodeLabelRequest request, List<BarcodeLabelItem> labels)
    {
        float widthMm = request.Layout == BarcodeLabelLayout.Thermal40x25 ? 40f : 50f;
        float heightMm = request.Layout == BarcodeLabelLayout.Thermal40x25 ? 25f : 30f;

        foreach (var labelItem in labels)
        {
            container.Page(page =>
            {
                page.ContinuousSize(widthMm, Unit.Millimetre);
                page.Margin(1.5f, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Content().Element(cell =>
                {
                    RenderSingleLabel(cell, request, labelItem, isThermal: true);
                });
            });
        }
    }

    private void RenderSingleLabel(IContainer container, BarcodeLabelRequest request, BarcodeLabelItem item, bool isThermal)
    {
        container.Column(col =>
        {
            col.Spacing(1);

            // 1. Shop Name
            if (request.IncludeShopName && !string.IsNullOrWhiteSpace(request.ShopName))
            {
                col.Item().AlignCenter().Text(request.ShopName)
                    .FontSize(isThermal ? 6 : 7).FontColor("#64748B").Bold();
            }

            // 2. Product Name
            if (request.IncludeProductName)
            {
                col.Item().AlignCenter().Text(text =>
                {
                    text.Span(item.ProductName).FontSize(isThermal ? 7.5f : 8.5f).Bold();
                });
            }

            // 3. Price
            if (request.IncludePrice && !string.IsNullOrWhiteSpace(item.FormattedPrice))
            {
                col.Item().AlignCenter().Text(item.FormattedPrice)
                    .FontSize(isThermal ? 8.5f : 9.5f).FontColor("#0F172A").Bold();
            }

            // 4. Barcode Vector Bars
            col.Item().PaddingTop(1).AlignCenter().Element(barContainer =>
            {
                bool[] pattern = GetBarcodePattern(item.Barcode, item.Format);
                if (pattern.Length > 0)
                {
                    RenderVectorBarcode(barContainer, pattern, isThermal ? 16f : 20f);
                }
            });

            // 5. Human-readable Barcode Text
            if (request.IncludeHumanReadableText && !string.IsNullOrWhiteSpace(item.Barcode))
            {
                col.Item().AlignCenter().Text(item.Barcode)
                    .FontFamily("Consolas").FontSize(isThermal ? 6.5f : 7f).LetterSpacing(0.08f);
            }
        });
    }

    internal static void RenderVectorBarcode(IContainer container, bool[] pattern, float heightPoints, float unitWidth = 0.85f)
    {
        // Run-length encode pattern into alternating black bars and white gaps
        var runs = new List<(bool isBlack, int count)>();
        if (pattern.Length > 0)
        {
            bool current = pattern[0];
            int count = 1;
            for (int i = 1; i < pattern.Length; i++)
            {
                if (pattern[i] == current)
                {
                    count++;
                }
                else
                {
                    runs.Add((current, count));
                    current = pattern[i];
                    count = 1;
                }
            }
            runs.Add((current, count));
        }

        // Render vector line bars
        container.Height(heightPoints).Row(row =>
        {
            foreach (var (isBlack, count) in runs)
            {
                if (isBlack)
                {
                    row.AutoItem().Width(count * unitWidth).Height(heightPoints).Background(Colors.Black);
                }
                else
                {
                    row.AutoItem().Width(count * unitWidth).Height(heightPoints);
                }
            }
        });
    }
}
