using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ShopPOS.Data.Converters;

/// <summary>
/// Converts decimal monetary values to and from 64-bit integer minor units (value * 100, e.g. cents)
/// for high-precision, exact SQL comparisons and aggregates in SQLite.
/// </summary>
public class MoneyConverter : ValueConverter<decimal, long>
{
    public MoneyConverter() : base(
        v => (long)Math.Round(v * 100m, MidpointRounding.AwayFromZero),
        v => (decimal)v / 100m)
    {
    }
}

