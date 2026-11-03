using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.ContactPerson)
            .HasMaxLength(150);

        builder.Property(s => s.Phone)
            .HasMaxLength(50);

        builder.Property(s => s.Email)
            .HasMaxLength(150);

        builder.Property(s => s.TaxNumber)
            .HasMaxLength(100);

        builder.Property(s => s.Address)
            .HasMaxLength(300);

        builder.Property(s => s.Notes)
            .HasMaxLength(1000);

        builder.Property(s => s.OpeningBalance)
            .HasConversion<MoneyConverter>();

        builder.Property(s => s.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(s => new { s.Name, s.Phone })
            .IsUnique()
            .HasFilter("IsDeleted = 0");
    }
}
