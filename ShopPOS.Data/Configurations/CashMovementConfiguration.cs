using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("CashMovements");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Amount)
            .HasConversion<MoneyConverter>();

        builder.Property(c => c.Reason)
            .IsRequired()
            .HasMaxLength(300);

        builder.HasIndex(c => c.ShiftId);

        builder.HasOne(c => c.Shift)
            .WithMany(s => s.CashMovements)
            .HasForeignKey(c => c.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

