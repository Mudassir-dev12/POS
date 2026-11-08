using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;
using ShopPOS.Data.Converters;

namespace ShopPOS.Data.Configurations;

public class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("SalePayments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount)
            .HasConversion<MoneyConverter>();

        builder.Property(p => p.TenderedAmount)
            .HasConversion<MoneyConverter>();

        builder.Property(p => p.Reference)
            .HasMaxLength(100);

        builder.HasIndex(p => p.SaleId);
        builder.HasIndex(p => new { p.Method, p.PaidAt });
    }
}

