using System;
using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.Diagnostics;

/// <summary>
/// Diagnostic system and database report data bundled in support zip archives.
/// Strictly sanitized: no passwords, salts, tokens, database files, or private customer data.
/// </summary>
public class DiagnosticsReportDto
{
    public string AppVersion { get; set; } = "1.0.0";
    public string OsVersion { get; set; } = string.Empty;
    public string DotNetVersion { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public DateTime GeneratedAtLocal { get; set; } = DateTime.Now;
    public string DataDirectory { get; set; } = string.Empty;
    public string DatabaseSchemaVersion { get; set; } = string.Empty;
    public long DatabaseSizeBytes { get; set; }
    public Dictionary<string, int> TableRowCounts { get; set; } = new();
    public SanitizedSettingsDto Settings { get; set; } = new();
}

public class SanitizedSettingsDto
{
    public string ShopName { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = "Rs";
    public int CurrencyDecimals { get; set; } = 2;
    public decimal DefaultTaxPercent { get; set; }
    public bool PricesIncludeTax { get; set; }
    public decimal CashRoundingStep { get; set; }
    public int ReceiptWidthMm { get; set; } = 80;
    public string PrinterMode { get; set; } = "PdfPreview";
    public bool AutoPrintReceipt { get; set; } = true;
    public bool BackupEnabled { get; set; } = true;
    public string BackupTime { get; set; } = "22:00";
    public bool BackupOnAppClose { get; set; } = true;
    public int KeepDaily { get; set; } = 7;
    public int KeepWeekly { get; set; } = 4;
    public int KeepMonthly { get; set; } = 6;
    public bool EncryptBackups { get; set; }
    public bool HasSecondFolder { get; set; }
    public bool GoogleDriveEnabled { get; set; }
    public bool IsGoogleDriveConnected { get; set; }
}

