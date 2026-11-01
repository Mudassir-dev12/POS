using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class StockAdjustmentItemConfiguration : IEntityTypeConfiguration<StockAdjustmentItem>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.SystemQuantity)
            .HasConversion<QuantityConverter>();

        builder.Property(i => i.NewQuantity)
            .HasConversion<QuantityConverter>();

        builder.Property(i => i.Difference)
            .HasConversion<QuantityConverter>();

        builder.Property(i => i.UnitCost)
            .HasConversion<MoneyConverter>();

        builder.Property(i => i.Note)
            .HasMaxLength(250);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

