namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Execution result of a database restore operation.
/// </summary>
public class RestoreResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PreRestoreBackupFileName { get; set; }
    public int RestoredProducts { get; set; }
    public int RestoredSales { get; set; }
    public int RestoredPurchases { get; set; }
    public bool RequiresRestart { get; set; } = true;
    public string SummaryMessage { get; set; } = string.Empty;
}

