namespace ShopPOS.Core.Enums;

/// <summary>
/// Specifies the origin or triggering intent for a database backup.
/// </summary>
public enum BackupKind
{
    Scheduled = 1,
    Manual = 2,
    PreMigration = 3,
    PreRestore = 4,
    PreDataTool = 5
}

