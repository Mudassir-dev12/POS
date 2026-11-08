using ShopPOS.Core.Enums;

namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Result of verifying a backup archive against checksum and SQLite integrity checks.
/// </summary>
public class BackupVerifyResultDto
{
    public bool IsValid { get; set; }
    public BackupVerifyStatus Status { get; set; } = BackupVerifyStatus.NotVerified;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, int> RowCounts { get; set; } = new();
    public string? SchemaVersion { get; set; }
}

