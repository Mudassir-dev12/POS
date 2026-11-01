using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class ProductPriceHistoryConfiguration : IEntityTypeConfiguration<ProductPriceHistory>
{
    public void Configure(EntityTypeBuilder<ProductPriceHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.OldCostPrice)
            .HasConversion<MoneyConverter>();

        builder.Property(h => h.NewCostPrice)
            .HasConversion<MoneyConverter>();

        builder.Property(h => h.OldSalePrice)
            .HasConversion<MoneyConverter>();

        builder.Property(h => h.NewSalePrice)
            .HasConversion<MoneyConverter>();

        builder.HasIndex(h => h.ProductId);
        builder.HasIndex(h => h.ChangedAt);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

