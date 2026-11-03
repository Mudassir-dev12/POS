using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        builder.ToTable("SaleReturns");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReturnNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.ReturnNumber)
            .IsUnique();

        builder.HasIndex(r => r.SaleId);

        builder.Property(r => r.SubTotal).HasConversion<MoneyConverter>();
        builder.Property(r => r.TaxAmount).HasConversion<MoneyConverter>();
        builder.Property(r => r.TotalAmount).HasConversion<MoneyConverter>();
        builder.Property(r => r.RefundedAmount).HasConversion<MoneyConverter>();

        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.CancelReason).HasMaxLength(500);

        builder.HasOne(r => r.Sale)
            .WithMany(s => s.Returns)
            .HasForeignKey(r => r.SaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Items)
            .WithOne(i => i.SaleReturn)
            .HasForeignKey(i => i.SaleReturnId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
