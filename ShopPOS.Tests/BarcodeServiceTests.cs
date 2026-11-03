using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Models;
using ShopPOS.Data;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class BarcodeServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly BarcodeService _service;

    public BarcodeServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        _service = new BarcodeService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData("890103000001", 0)] // 8901030000010
    [InlineData("890103000002", 7)] // 8901030000027
    [InlineData("400638133393", 1)] // 4006381333931
    [InlineData("500011263792", 2)] // 5000112637922
    public void CalculateEan13CheckDigit_ValidTwelveDigits_CalculatesCorrectCheckDigit(string twelveDigits, int expectedCheck)
    {
        int actual = _service.CalculateEan13CheckDigit(twelveDigits);
        actual.Should().Be(expectedCheck);
    }

    [Theory]
    [InlineData("01234567890", 5)] // 012345678905
    [InlineData("03600029145", 2)] // 036000291452
    public void CalculateUpcACheckDigit_ValidElevenDigits_CalculatesCorrectCheckDigit(string elevenDigits, int expectedCheck)
    {
        int actual = _service.CalculateUpcACheckDigit(elevenDigits);
        actual.Should().Be(expectedCheck);
    }

    [Fact]
    public void ValidateEan13_ValidAndInvalidChecksums()
    {
        _service.ValidateEan13("8901030000010").Should().BeTrue();
        _service.ValidateEan13("8901030000019").Should().BeFalse();
        _service.ValidateEan13("12345").Should().BeFalse();
    }

    [Fact]
    public void ValidateUpcA_ValidAndInvalidChecksums()
    {
        _service.ValidateUpcA("012345678905").Should().BeTrue();
        _service.ValidateUpcA("012345678906").Should().BeFalse();
        _service.ValidateUpcA("12345").Should().BeFalse();
    }

    [Fact]
    public void ValidateCode128_ValidAndInvalidStrings()
    {
        _service.ValidateCode128("SKU-9901-PAK").Should().BeTrue();
        _service.ValidateCode128("").Should().BeFalse();
    }

    [Fact]
    public void GenerateEan13_ProducesValid13DigitWithCorrectCheckDigit()
    {
        string ean = _service.GenerateEan13("89");
        ean.Should().HaveLength(13);
        ean.StartsWith("89").Should().BeTrue();
        _service.ValidateEan13(ean).Should().BeTrue();
    }

    [Fact]
    public void GenerateCode128_ProducesValidAlphanumericString()
    {
        string code = _service.GenerateCode128("PRD");
        code.Should().NotBeNullOrWhiteSpace();
        code.StartsWith("PRD").Should().BeTrue();
        _service.ValidateCode128(code).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateUniqueBarcodeAsync_ProducesUniqueBarcodeNotCollidingWithDatabase()
    {
        var unit = new Unit { Name = "Pieces", ShortName = "pcs", IsActive = true };
        _context.Units.Add(unit);

        var category = new Category { Name = "Beverages", IsActive = true };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var product = new Product
        {
            Name = "Existing Beverage",
            Sku = "P999999",
            Barcode = "2000000000019",
            CostPrice = 10m,
            SalePrice = 15m,
            UnitId = unit.Id,
            CategoryId = category.Id,
            IsActive = true
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        string uniqueEan = await _service.GenerateUniqueBarcodeAsync(BarcodeFormat.Ean13);
        uniqueEan.Should().NotBeNullOrWhiteSpace();
        uniqueEan.Should().NotBe("2000000000019");
        _service.ValidateEan13(uniqueEan).Should().BeTrue();
    }

    [Fact]
    public void GetBarcodePattern_ReturnsNonEmptyBinaryPatternForRendering()
    {
        var patternEan = _service.GetBarcodePattern("8901030000010", BarcodeFormat.Ean13);
        patternEan.Should().NotBeEmpty();
        patternEan.Should().HaveCount(113); // EAN-13: 11 (left quiet) + 3 + 42 + 5 + 42 + 3 + 9 (right quiet) = 115

        var patternCode128 = _service.GetBarcodePattern("ITEM100", BarcodeFormat.Code128);
        patternCode128.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateBarcodeLabelPdf_ProducesValidPdfBytes()
    {
        var request = new BarcodeLabelRequest
        {
            ShopName = "Super Mart",
            Layout = BarcodeLabelLayout.A4Sheet30Up,
            Items = new List<BarcodeLabelItem>
            {
                new BarcodeLabelItem
                {
                    ProductName = "Coca Cola 500ml",
                    Barcode = "8901030000010",
                    Format = BarcodeFormat.Ean13,
                    Price = 75.00m,
                    FormattedPrice = "Rs 75.00",
                    Quantity = 3
                },
                new BarcodeLabelItem
                {
                    ProductName = "Lays Classic 45g",
                    Barcode = "8901030000027",
                    Format = BarcodeFormat.Ean13,
                    Price = 60.00m,
                    FormattedPrice = "Rs 60.00",
                    Quantity = 2
                }
            }
        };

        byte[] pdfBytes = _service.GenerateBarcodeLabelPdf(request);

        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(100);
        // Standard PDF file signature is %PDF
        pdfBytes[0].Should().Be((byte)'%');
        pdfBytes[1].Should().Be((byte)'P');
        pdfBytes[2].Should().Be((byte)'D');
        pdfBytes[3].Should().Be((byte)'F');
    }
}
