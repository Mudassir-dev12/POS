namespace ShopPOS.Core.Enums;

/// <summary>
/// Status of SQLite integrity checks and manifest verification on a backup.
/// </summary>
public enum BackupVerifyStatus
{
    NotVerified = 0,
    Ok = 1,
    Failed = 2
}

