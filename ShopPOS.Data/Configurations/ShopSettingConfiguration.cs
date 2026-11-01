using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;

namespace ShopPOS.Data.Configurations;

public class ShopSettingConfiguration : IEntityTypeConfiguration<ShopSetting>
{
    public void Configure(EntityTypeBuilder<ShopSetting> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ShopName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.CurrencySymbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(s => s.DefaultTaxPercent)
            .HasPrecision(5, 2);

        builder.Property(s => s.ReceiptFooter)
            .HasMaxLength(300);

        builder.Property(s => s.DefaultReorderMultiplier)
            .HasDefaultValue(2.0m)
            .HasPrecision(5, 2);
    }
}

