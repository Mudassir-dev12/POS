using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Quantity)
            .HasConversion<QuantityConverter>();

        builder.Property(s => s.UnitCost)
            .HasConversion<MoneyConverter>();

        builder.Property(s => s.ReferenceType)
            .HasMaxLength(100);

        builder.Property(s => s.Note)
            .HasMaxLength(500);

        // Required indexes
        builder.HasIndex(s => s.ProductId);
        builder.HasIndex(s => s.MovementDate);
        builder.HasIndex(s => new { s.ProductId, s.MovementDate });
        builder.HasIndex(s => s.MovementType);
        builder.HasIndex(s => new { s.ReferenceType, s.ReferenceId });

        builder.HasOne(s => s.Product)
            .WithMany(p => p.StockMovements)
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

