using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ShopPOS.Data.Converters;

/// <summary>
/// Converts nullable decimal quantity values to and from nullable 64-bit integer thousandths.
/// </summary>
public class NullableQuantityConverter : ValueConverter<decimal?, long?>
{
    public NullableQuantityConverter() : base(
        v => v.HasValue ? (long)Math.Round(v.Value * 1000m, MidpointRounding.AwayFromZero) : null,
        v => v.HasValue ? (decimal)v.Value / 1000m : null)
    {
    }
}

