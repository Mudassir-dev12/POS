using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PurchaseNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => p.PurchaseNumber)
            .IsUnique();

        builder.Property(p => p.SupplierInvoiceNo)
            .HasMaxLength(100);

        // Unique index on (SupplierId, SupplierInvoiceNo) when SupplierInvoiceNo is not empty
        builder.HasIndex(p => new { p.SupplierId, p.SupplierInvoiceNo })
            .IsUnique()
            .HasFilter("[SupplierInvoiceNo] IS NOT NULL AND [SupplierInvoiceNo] <> ''");

        builder.HasIndex(p => p.SupplierId);
        builder.HasIndex(p => p.PurchaseDate);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => new { p.Status, p.PurchaseDate });

        builder.Property(p => p.SubTotal)
            .HasConversion<MoneyConverter>();

        builder.Property(p => p.DiscountAmount)
            .HasConversion<MoneyConverter>();

        builder.Property(p => p.TaxAmount)
            .HasConversion<MoneyConverter>();

        builder.Property(p => p.FreightAmount)
            .HasConversion<MoneyConverter>();

        builder.Property(p => p.GrandTotal)
            .HasConversion<MoneyConverter>();

        builder.Property(p => p.PaidAmount)
            .HasConversion<MoneyConverter>();

        builder.Property(p => p.Notes)
            .HasMaxLength(1000);

        builder.Property(p => p.CancelReason)
            .HasMaxLength(500);

        builder.HasOne(p => p.Supplier)
            .WithMany(s => s.Purchases)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.PostedByUser)
            .WithMany()
            .HasForeignKey(p => p.PostedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.CancelledByUser)
            .WithMany()
            .HasForeignKey(p => p.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

