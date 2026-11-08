namespace ShopPOS.Core.Enums;

/// <summary>
/// Status of cloud synchronization for a backup archive (placeholder for Phase 7B).
/// </summary>
public enum DriveUploadStatus
{
    NotConfigured = 0,
    Pending = 1,
    Uploading = 2,
    Uploaded = 3,
    Failed = 4
}

