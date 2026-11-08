namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Manifest metadata stored inside each backup archive as manifest.json.
/// </summary>
public class BackupManifest
{
    public string AppVersion { get; set; } = "1.0.0";
    public string SchemaVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string MachineName { get; set; } = Environment.MachineName;
    public string ShopName { get; set; } = "Shop POS";
    public Dictionary<string, int> TableRowCounts { get; set; } = new();
    public string DatabaseSha256 { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
}

