using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;

namespace ShopPOS.Data.Configurations;

public class ProductBarcodeConfiguration : IEntityTypeConfiguration<ProductBarcode>
{
    public void Configure(EntityTypeBuilder<ProductBarcode> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Barcode)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(b => b.Barcode)
            .IsUnique()
            .HasFilter("IsDeleted = 0");

        builder.HasIndex(b => b.ProductId);
    }
}

