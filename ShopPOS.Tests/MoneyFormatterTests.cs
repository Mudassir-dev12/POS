using System;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;
using Xunit;

namespace ShopPOS.Tests;

public class MoneyFormatterTests
{
    [Fact]
    public void Format_DefaultSettings_FormatsWithRsAndTwoDecimals()
    {
        var formatter = new MoneyFormatter("Rs", 2);
        string formatted = formatter.Format(1234.50m);
        Assert.Equal("Rs 1,234.50", formatted);
    }

    [Fact]
    public void Format_ZeroDecimals_RoundsToNearestInteger()
    {
        var formatter = new MoneyFormatter("Rs", 0);
        Assert.Equal("Rs 1,235", formatter.Format(1234.50m));
        Assert.Equal("Rs 1,234", formatter.Format(1234.49m));
    }

    [Fact]
    public void Format_OneDecimal_FormatsWithOneDecimal()
    {
        var formatter = new MoneyFormatter("Rs", 1);
        Assert.Equal("Rs 1,234.5", formatter.Format(1234.50m));
    }

    [Fact]
    public void Format_ThreeDecimals_FormatsWithThreeDecimals()
    {
        var formatter = new MoneyFormatter("Rs", 3);
        Assert.Equal("Rs 1,234.500", formatter.Format(1234.50m));
    }

    [Fact]
    public void Format_NegativeValue_PlacesMinusAfterSymbol()
    {
        var formatter = new MoneyFormatter("Rs", 2);
        string formatted = formatter.Format(-1234.50m);
        Assert.Equal("Rs -1,234.50", formatted);
    }

    [Fact]
    public void Format_LargeNumbers_UsesThousandsSeparators()
    {
        var formatter = new MoneyFormatter("Rs", 2);
        Assert.Equal("Rs 10,000,000.00", formatter.Format(10000000m));
    }

    [Fact]
    public void Format_RoundingHalfAwayFromZero_RoundsCorrectly()
    {
        var formatter = new MoneyFormatter("Rs", 2);
        Assert.Equal("Rs 1.24", formatter.Format(1.235m));
        Assert.Equal("Rs 1.23", formatter.Format(1.234m));
    }

    [Fact]
    public void Format_CustomSymbol_UpdatesPrefix()
    {
        var formatter = new MoneyFormatter("PKR", 2);
        Assert.Equal("PKR 500.00", formatter.Format(500m));
    }

    [Fact]
    public void FormatNoSymbol_OmitsCurrencyPrefix()
    {
        var formatter = new MoneyFormatter("Rs", 2);
        Assert.Equal("1,234.50", formatter.FormatNoSymbol(1234.50m));
    }

    [Fact]
    public void FormatDate_StandardDate_OutputsDayMonthYear()
    {
        var formatter = new MoneyFormatter("Rs", 2);
        var date = new DateTime(2026, 11, 2, 14, 30, 0);
        Assert.Equal("02 Nov 2026", formatter.FormatDate(date));
    }

    [Fact]
    public void FormatTime_StandardTime_Outputs12HourTime()
    {
        var formatter = new MoneyFormatter("Rs", 2);
        var time = new DateTime(2026, 11, 2, 14, 30, 0);
        Assert.Equal("02:30 PM", formatter.FormatTime(time));
    }

    [Fact]
    public void FormatDateTime_StandardDateTime_OutputsFormattedDateAndTime()
    {
        var formatter = new MoneyFormatter("Rs", 2);
        var dt = new DateTime(2026, 11, 2, 9, 15, 0);
        Assert.Equal("02 Nov 2026, 09:15 AM", formatter.FormatDateTime(dt));
    }
}
