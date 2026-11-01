using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ShiftNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(s => s.ShiftNumber)
            .IsUnique();

        builder.Property(s => s.OpeningCash)
            .HasConversion<MoneyConverter>();

        builder.Property(s => s.CountedCash)
            .HasConversion<MoneyConverter>();

        builder.Property(s => s.ExpectedCash)
            .HasConversion<MoneyConverter>();

        builder.Property(s => s.Difference)
            .HasConversion<MoneyConverter>();

        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        // Filtered unique index: only one Open (1) shift allowed at any time
        builder.HasIndex(s => s.Status)
            .IsUnique()
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");
    }
}

