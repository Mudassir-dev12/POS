using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;

namespace ShopPOS.Data.Configurations;

public class ShopSettingConfiguration : IEntityTypeConfiguration<ShopSetting>
{
    public void Configure(EntityTypeBuilder<ShopSetting> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ShopName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.CurrencySymbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(s => s.DefaultTaxPercent)
            .HasPrecision(5, 2);

        builder.Property(s => s.ReceiptFooter)
            .HasMaxLength(300);

        builder.Property(s => s.DefaultReorderMultiplier)
            .HasDefaultValue(2.0m)
            .HasPrecision(5, 2);

        builder.Property(s => s.RequireOpenShiftForSales)
            .HasDefaultValue(true);

        builder.Property(s => s.AllowCreditSales)
            .HasDefaultValue(true);

        builder.Property(s => s.AllowSellingBelowMinPrice)
            .HasDefaultValue(false);

        builder.Property(s => s.PricesIncludeTax)
            .HasDefaultValue(false);

        builder.Property(s => s.CashRoundingStep)
            .HasPrecision(5, 2)
            .HasDefaultValue(0m);

        builder.Property(s => s.AllowReturnWithoutReceipt)
            .HasDefaultValue(false);

        builder.Property(s => s.AutoPrintReceipt)
            .HasDefaultValue(true);

        builder.Property(s => s.ReceiptPrinterName)
            .HasMaxLength(150);

        builder.Property(s => s.OpenCashDrawerOnCashSale)
            .HasDefaultValue(false);

        builder.Property(s => s.ReceiptCopies)
            .HasDefaultValue(1);

        builder.Property(s => s.PrinterMode)
            .HasDefaultValue(ShopPOS.Core.Enums.PrinterMode.PdfPreview);
    }
}
