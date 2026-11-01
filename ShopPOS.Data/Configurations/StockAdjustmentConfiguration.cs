using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;

namespace ShopPOS.Data.Configurations;

public class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AdjustmentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(a => a.AdjustmentNumber)
            .IsUnique();

        builder.Property(a => a.Notes)
            .HasMaxLength(500);

        builder.Property(a => a.CancelReason)
            .HasMaxLength(500);

        // Required indexes
        builder.HasIndex(a => new { a.Status, a.AdjustmentDate });

        builder.HasMany(a => a.Items)
            .WithOne(i => i.StockAdjustment)
            .HasForeignKey(i => i.StockAdjustmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.PostedByUser)
            .WithMany()
            .HasForeignKey(a => a.PostedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CancelledByUser)
            .WithMany()
            .HasForeignKey(a => a.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

