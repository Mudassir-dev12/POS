using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
{
    public void Configure(EntityTypeBuilder<PurchaseReturn> builder)
    {
        builder.ToTable("PurchaseReturns");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReturnNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.ReturnNumber)
            .IsUnique();

        builder.HasIndex(r => r.SupplierId);

        builder.Property(r => r.TotalAmount)
            .HasConversion<MoneyConverter>();

        builder.Property(r => r.Notes)
            .HasMaxLength(1000);

        builder.Property(r => r.CancelReason)
            .HasMaxLength(500);

        builder.HasOne(r => r.Supplier)
            .WithMany(s => s.Returns)
            .HasForeignKey(r => r.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Purchase)
            .WithMany(p => p.Returns)
            .HasForeignKey(r => r.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.PostedByUser)
            .WithMany()
            .HasForeignKey(r => r.PostedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CancelledByUser)
            .WithMany()
            .HasForeignKey(r => r.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

