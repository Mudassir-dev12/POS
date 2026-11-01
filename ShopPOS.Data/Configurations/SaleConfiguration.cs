using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SaleNumber)
            .HasMaxLength(50);

        builder.HasIndex(s => s.SaleNumber)
            .IsUnique()
            .HasFilter("[SaleNumber] IS NOT NULL");

        builder.Property(s => s.HoldNumber)
            .HasMaxLength(50);

        builder.HasIndex(s => s.HoldNumber);

        builder.HasIndex(s => new { s.Status, s.SaleDate });
        builder.HasIndex(s => s.CustomerId);
        builder.HasIndex(s => s.ShiftId);

        builder.Property(s => s.SubTotal).HasConversion<MoneyConverter>();
        builder.Property(s => s.LineDiscountTotal).HasConversion<MoneyConverter>();
        builder.Property(s => s.BillDiscountAmount).HasConversion<MoneyConverter>();
        builder.Property(s => s.TaxAmount).HasConversion<MoneyConverter>();
        builder.Property(s => s.RoundOff).HasConversion<MoneyConverter>();
        builder.Property(s => s.GrandTotal).HasConversion<MoneyConverter>();
        builder.Property(s => s.PaidAmount).HasConversion<MoneyConverter>();
        builder.Property(s => s.ChangeAmount).HasConversion<MoneyConverter>();
        builder.Property(s => s.DueAmount).HasConversion<MoneyConverter>();
        builder.Property(s => s.TotalCost).HasConversion<MoneyConverter>();

        builder.Property(s => s.Notes).HasMaxLength(1000);
        builder.Property(s => s.CancelReason).HasMaxLength(500);

        builder.HasOne(s => s.Customer)
            .WithMany(c => c.Sales)
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.PriceLevel)
            .WithMany()
            .HasForeignKey(s => s.PriceLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Shift)
            .WithMany(sh => sh.Sales)
            .HasForeignKey(s => s.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Items)
            .WithOne(i => i.Sale)
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Payments)
            .WithOne(p => p.Sale)
            .HasForeignKey(p => p.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Returns)
            .WithOne(r => r.Sale)
            .HasForeignKey(r => r.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

