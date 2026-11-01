using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class SaleReturnItemConfiguration : IEntityTypeConfiguration<SaleReturnItem>
{
    public void Configure(EntityTypeBuilder<SaleReturnItem> builder)
    {
        builder.ToTable("SaleReturnItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.Barcode)
            .HasMaxLength(50);

        builder.Property(i => i.Quantity)
            .HasConversion<QuantityConverter>();

        builder.Property(i => i.UnitPrice)
            .HasConversion<MoneyConverter>();

        builder.Property(i => i.CostPriceSnapshot)
            .HasConversion<MoneyConverter>();

        builder.Property(i => i.TaxPercent)
            .HasPrecision(5, 2);

        builder.Property(i => i.LineTotal)
            .HasConversion<MoneyConverter>();

        builder.Property(i => i.ReturnToStock)
            .HasDefaultValue(true);

        builder.HasIndex(i => i.SaleReturnId);
        builder.HasIndex(i => i.SaleItemId);
        builder.HasIndex(i => new { i.SaleReturnId, i.SaleItemId });
        builder.HasIndex(i => i.ProductId);

        builder.HasOne(i => i.SaleItem)
            .WithMany()
            .HasForeignKey(i => i.SaleItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

