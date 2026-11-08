using ShopPOS.Core.Common;

namespace ShopPOS.Core.Entities;

/// <summary>
/// Represents store-wide configuration and receipt details.
/// </summary>
public class ShopSetting : BaseEntity
{
    /// <summary>
    /// Gets or sets the display name of the shop.
    /// </summary>
    public string ShopName { get; set; } = "Shop POS";

    /// <summary>
    /// Gets or sets the physical shop address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets the shop primary telephone number.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the secondary telephone or mobile/WhatsApp number.
    /// </summary>
    public string? SecondaryPhone { get; set; }

    /// <summary>
    /// Gets or sets the path to the store logo image printed on receipts.
    /// </summary>
    public string? LogoImagePath { get; set; }

    /// <summary>
    /// Gets or sets the store policies (e.g. return/exchange terms) printed at the bottom of receipts.
    /// </summary>
    public string? StorePolicy { get; set; } = "Items can be exchanged within 7 days with original receipt. No cash refunds.";

    /// <summary>
    /// Gets or sets the tax / VAT registration number.
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Gets or sets the currency symbol (default "Rs").
    /// </summary>
    public string CurrencySymbol { get; set; } = "Rs";

    /// <summary>
    /// Gets or sets the currency decimal places (0 to 3, default 2).
    /// </summary>
    public int CurrencyDecimals { get; set; } = 2;

    /// <summary>
    /// Gets or sets the custom text printed at the bottom of receipts.
    /// </summary>
    public string? ReceiptFooter { get; set; } = "Thank you for your business!";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? ReceiptFooterText { get => ReceiptFooter; set => ReceiptFooter = value; }

    /// <summary>
    /// Gets or sets the thermal receipt printer paper width in mm (58 or 80).
    /// </summary>
    public int ReceiptWidthMm { get; set; } = 80;

    /// <summary>
    /// Gets or sets the receipt printing output mode (PdfPreview, WindowsDriver, EscPosRaw). Default is PdfPreview.
    /// </summary>
    public ShopPOS.Core.Enums.PrinterMode PrinterMode { get; set; } = ShopPOS.Core.Enums.PrinterMode.PdfPreview;

    /// <summary>
    /// Gets or sets the default sales tax percentage.
    /// </summary>
    public decimal DefaultTaxPercent { get; set; } = 0m;

    /// <summary>
    /// Gets or sets the inventory costing method (WeightedAverage or LastCost). Default is WeightedAverage.
    /// </summary>
    public ShopPOS.Core.Enums.CostingMethod CostingMethod { get; set; } = ShopPOS.Core.Enums.CostingMethod.WeightedAverage;

    /// <summary>
    /// Gets or sets whether inventory levels are allowed to become negative. Default is false.
    /// </summary>
    public bool AllowNegativeStock { get; set; } = false;

    /// <summary>
    /// Gets or sets the default multiplier applied to ReorderLevel when calculating suggested restock quantities. Default is 2.
    /// </summary>
    public decimal DefaultReorderMultiplier { get; set; } = 2.0m;

    /// <summary>
    /// Gets or sets whether selling below the product's MinSalePrice is permitted. Default is false.
    /// </summary>
    public bool AllowSellingBelowMinPrice { get; set; } = false;

    /// <summary>
    /// Gets or sets whether catalog selling prices already include tax. Default is false.
    /// </summary>
    public bool PricesIncludeTax { get; set; } = false;

    /// <summary>
    /// Gets or sets the cash rounding denomination step (e.g. 0.05, 1, 5). 0 = rounding disabled. Default is 0.
    /// </summary>
    public decimal CashRoundingStep { get; set; } = 0m;

    /// <summary>
    /// Gets or sets whether returns can be processed without referencing the original sale receipt. Default is false.
    /// </summary>
    public bool AllowReturnWithoutReceipt { get; set; } = false;

    /// <summary>
    /// Gets or sets whether receipts are automatically printed upon sale completion. Default is true.
    /// </summary>
    public bool AutoPrintReceipt { get; set; } = true;

    /// <summary>
    /// Gets or sets the configured thermal receipt printer name.
    /// </summary>
    public string? ReceiptPrinterName { get; set; }

    /// <summary>
    /// Gets or sets whether the cash drawer kick pulse is sent on cash sales. Default is false.
    /// </summary>
    public bool OpenCashDrawerOnCashSale { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of receipt copies to print per transaction. Default is 1.
    /// </summary>
    public int ReceiptCopies { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum window in days for customer returns against a receipt. Null = unlimited.
    /// </summary>
    public int? SaleReturnWindowDays { get; set; }

    /// <summary>
    /// Gets or sets whether automatic scheduled backups are enabled. Default is true.
    /// </summary>
    public bool BackupEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the daily scheduled backup time in HH:mm 24-hour format (default 22:00).
    /// </summary>
    public string BackupTime { get; set; } = "22:00";

    /// <summary>
    /// Gets or sets whether an automatic backup is taken on application close. Default is true.
    /// </summary>
    public bool BackupOnAppClose { get; set; } = true;

    /// <summary>
    /// Gets or sets how many daily backups to retain under GFS retention. Default is 7.
    /// </summary>
    public int KeepDaily { get; set; } = 7;

    /// <summary>
    /// Gets or sets how many weekly backups to retain under GFS retention. Default is 4.
    /// </summary>
    public int KeepWeekly { get; set; } = 4;

    /// <summary>
    /// Gets or sets how many monthly backups to retain under GFS retention. Default is 6.
    /// </summary>
    public int KeepMonthly { get; set; } = 6;

    /// <summary>
    /// Gets or sets the optional secondary folder path for mirroring backups (e.g. external USB drive).
    /// </summary>
    public string? SecondFolderPath { get; set; }

    /// <summary>
    /// Gets or sets whether backups are encrypted with AES-256-GCM. Default is false.
    /// </summary>
    public bool EncryptBackups { get; set; } = false;

    /// <summary>
    /// Gets or sets the random salt for PBKDF2 encryption key derivation (base64).
    /// </summary>
    public string? EncryptionSalt { get; set; }

    /// <summary>
    /// Gets or sets the key-check hash value (never the password) to validate password entry.
    /// </summary>
    public string? EncryptionKeyCheck { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last successful backup.
    /// </summary>
    public DateTime? LastSuccessfulBackupAt { get; set; }

    /// <summary>
    /// Gets or sets the error message from the last backup attempt, if any.
    /// </summary>
    public string? LastBackupError { get; set; }

    /// <summary>
    /// Gets or sets whether automatic Google Drive uploads are enabled. Default is false.
    /// </summary>
    public bool GoogleDriveEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the connected Google account email.
    /// </summary>
    public string? GoogleDriveAccountEmail { get; set; }

    /// <summary>
    /// Gets or sets the Drive ID of the "ShopPOS Backups" root folder.
    /// </summary>
    public string? GoogleDriveFolderId { get; set; }

    /// <summary>
    /// Gets or sets the Drive ID of the shop subfolder inside "ShopPOS Backups".
    /// </summary>
    public string? GoogleDriveSubFolderId { get; set; }

    /// <summary>
    /// Gets or sets whether manual backups are also preserved on Drive during retention. Default is true.
    /// </summary>
    public bool KeepManualBackupsOnDrive { get; set; } = true;

    /// <summary>
    /// Gets or sets whether older backups purged by Drive retention are moved to trash instead of deleted permanently. Default is false (permanent delete).
    /// </summary>
    public bool GoogleDriveDeleteToTrash { get; set; } = false;

    /// <summary>
    /// Gets or sets the timestamp of the last successful upload to Google Drive.
    /// </summary>
    public DateTime? LastSuccessfulDriveUploadAt { get; set; }

    /// <summary>
    /// Gets or sets the error message from the last Google Drive attempt, if any.
    /// </summary>
    public string? LastDriveUploadError { get; set; }

    /// <summary>
    /// Gets or sets whether user re-authentication is required due to token expiration (401 / invalid_grant).
    /// </summary>
    public bool GoogleDriveReconnectNeeded { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the connected Google Drive account is full (403 storage quota exceeded).
    /// </summary>
    public bool GoogleDriveIsFull { get; set; } = false;

    // Phase 7B-GitHub Private Repository Backup

    /// <summary>
    /// Gets or sets whether automatic uploads to a private GitHub repository are enabled. Default is false.
    /// </summary>
    public bool GitHubEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the GitHub repository owner / organization name.
    /// </summary>
    public string? GitHubOwner { get; set; }

    /// <summary>
    /// Gets or sets the GitHub repository name.
    /// </summary>
    public string? GitHubRepo { get; set; }

    /// <summary>
    /// Gets or sets the target branch name (default is "main").
    /// </summary>
    public string GitHubBranch { get; set; } = "main";

    /// <summary>
    /// Gets or sets whether product images are excluded from the GitHub backup archive (default true to preserve quota).
    /// </summary>
    public bool GitHubExcludeImages { get; set; } = true;

    /// <summary>
    /// Gets or sets the timestamp of the last successful upload to GitHub.
    /// </summary>
    public DateTime? LastSuccessfulGitHubUploadAt { get; set; }

    /// <summary>
    /// Gets or sets the error message from the last GitHub upload attempt, if any.
    /// </summary>
    public string? LastGitHubUploadError { get; set; }

    /// <summary>
    /// Gets or sets whether re-authentication / new token is needed (401 / 403).
    /// </summary>
    public bool GitHubReconnectNeeded { get; set; } = false;

    /// <summary>
    /// Gets or sets whether upload was refused because the target GitHub repository is public.
    /// </summary>
    public bool GitHubRepoIsPublic { get; set; } = false;

    /// <summary>
    /// Gets or sets whether upload was refused because the backup package exceeded 90 MB.
    /// </summary>
    public bool GitHubFileTooLarge { get; set; } = false;

    /// <summary>
    /// Gets or sets the timestamp when commit history was last squashed.
    /// </summary>
    public DateTime? LastGitHubSquashAt { get; set; }
}
