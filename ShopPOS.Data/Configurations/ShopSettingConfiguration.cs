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
            .HasMaxLength(10)
            .HasDefaultValue("Rs");

        builder.Property(s => s.CurrencyDecimals)
            .HasDefaultValue(2);

        builder.Property(s => s.DefaultTaxPercent)
            .HasPrecision(5, 2);

        builder.Property(s => s.ReceiptFooter)
            .HasMaxLength(300);

        builder.Property(s => s.SecondaryPhone)
            .HasMaxLength(50);

        builder.Property(s => s.LogoImagePath)
            .HasMaxLength(500);

        builder.Property(s => s.StorePolicy)
            .HasMaxLength(1000);

        builder.Property(s => s.DefaultReorderMultiplier)
            .HasDefaultValue(2.0m)
            .HasPrecision(5, 2);

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

        builder.Property(s => s.BackupEnabled)
            .HasDefaultValue(true);

        builder.Property(s => s.BackupTime)
            .HasMaxLength(10)
            .HasDefaultValue("22:00");

        builder.Property(s => s.BackupOnAppClose)
            .HasDefaultValue(true);

        builder.Property(s => s.KeepDaily)
            .HasDefaultValue(7);

        builder.Property(s => s.KeepWeekly)
            .HasDefaultValue(4);

        builder.Property(s => s.KeepMonthly)
            .HasDefaultValue(6);

        builder.Property(s => s.SecondFolderPath)
            .HasMaxLength(500);

        builder.Property(s => s.EncryptBackups)
            .HasDefaultValue(false);

        builder.Property(s => s.EncryptionSalt)
            .HasMaxLength(200);

        builder.Property(s => s.EncryptionKeyCheck)
            .HasMaxLength(200);

        builder.Property(s => s.LastBackupError)
            .HasMaxLength(2000);

        // Phase 7B Google Drive configurations
        builder.Property(s => s.GoogleDriveEnabled)
            .HasDefaultValue(false);

        builder.Property(s => s.GoogleDriveAccountEmail)
            .HasMaxLength(200);

        builder.Property(s => s.GoogleDriveFolderId)
            .HasMaxLength(200);

        builder.Property(s => s.GoogleDriveSubFolderId)
            .HasMaxLength(200);

        builder.Property(s => s.KeepManualBackupsOnDrive)
            .HasDefaultValue(true);

        builder.Property(s => s.GoogleDriveDeleteToTrash)
            .HasDefaultValue(false);

        builder.Property(s => s.LastDriveUploadError)
            .HasMaxLength(2000);

        builder.Property(s => s.GoogleDriveReconnectNeeded)
            .HasDefaultValue(false);

        builder.Property(s => s.GoogleDriveIsFull)
            .HasDefaultValue(false);

        // Phase 7B-GitHub configurations
        builder.Property(s => s.GitHubEnabled)
            .HasDefaultValue(false);

        builder.Property(s => s.GitHubOwner)
            .HasMaxLength(150);

        builder.Property(s => s.GitHubRepo)
            .HasMaxLength(150);

        builder.Property(s => s.GitHubBranch)
            .HasMaxLength(100)
            .HasDefaultValue("main");

        builder.Property(s => s.GitHubExcludeImages)
            .HasDefaultValue(true);

        builder.Property(s => s.LastGitHubUploadError)
            .HasMaxLength(2000);

        builder.Property(s => s.GitHubReconnectNeeded)
            .HasDefaultValue(false);

        builder.Property(s => s.GitHubRepoIsPublic)
            .HasDefaultValue(false);

        builder.Property(s => s.GitHubFileTooLarge)
            .HasDefaultValue(false);
    }
}
