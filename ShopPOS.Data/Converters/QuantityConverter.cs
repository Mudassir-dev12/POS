using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ShopPOS.Data.Converters;

/// <summary>
/// Converts decimal quantity values to and from 64-bit integer thousandths (value * 1000)
/// in SQLite to support exact fractional weights (e.g. 1.250 kg = 1250 units).
/// </summary>
public class QuantityConverter : ValueConverter<decimal, long>
{
    public QuantityConverter() : base(
        v => (long)Math.Round(v * 1000m, MidpointRounding.AwayFromZero),
        v => (decimal)v / 1000m)
    {
    }
}

