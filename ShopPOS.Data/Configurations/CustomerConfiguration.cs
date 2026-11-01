using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Phone)
            .HasMaxLength(50);

        builder.Property(c => c.Email)
            .HasMaxLength(150);

        builder.Property(c => c.TaxNumber)
            .HasMaxLength(100);

        builder.Property(c => c.Address)
            .HasMaxLength(300);

        builder.Property(c => c.Notes)
            .HasMaxLength(1000);

        builder.Property(c => c.CreditLimit)
            .HasConversion<MoneyConverter>();

        builder.Property(c => c.OpeningBalance)
            .HasConversion<MoneyConverter>();

        builder.Property(c => c.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(c => c.Phone)
            .IsUnique()
            .HasFilter("[Phone] IS NOT NULL AND [Phone] != ''");

        builder.HasOne(c => c.DefaultPriceLevel)
            .WithMany(pl => pl.Customers)
            .HasForeignKey(c => c.DefaultPriceLevelId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
