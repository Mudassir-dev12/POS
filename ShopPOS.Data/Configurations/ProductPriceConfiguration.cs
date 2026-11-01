using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.HasKey(pp => pp.Id);

        builder.Property(pp => pp.Price)
            .HasConversion<MoneyConverter>();

        builder.HasIndex(pp => new { pp.ProductId, pp.PriceLevelId })
            .IsUnique();

        builder.HasOne(pp => pp.Product)
            .WithMany(p => p.ProductPrices)
            .HasForeignKey(pp => pp.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pp => pp.PriceLevel)
            .WithMany(pl => pl.ProductPrices)
            .HasForeignKey(pp => pp.PriceLevelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

