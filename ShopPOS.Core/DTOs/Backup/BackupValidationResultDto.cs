namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Pre-restore validation analysis comparing the backup archive against the running application and database.
/// </summary>
public class BackupValidationResultDto
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
    public bool RequiresPassword { get; set; }
    public BackupManifest? Manifest { get; set; }
    public Dictionary<string, int> CurrentDatabaseRowCounts { get; set; } = new();
    public string CurrentSchemaVersion { get; set; } = string.Empty;
    public string BackupSchemaVersion => Manifest?.SchemaVersion ?? string.Empty;
    public bool SchemaIsNewerThanApp { get; set; }
    public bool SchemaIsOlderThanApp { get; set; }
}

