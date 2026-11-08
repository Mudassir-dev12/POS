using ShopPOS.Core.DTOs.Backup;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Service responsible for loading OAuth client credentials from configuration file or environment variables.
/// </summary>
public interface IGoogleDriveConfigService
{
    GoogleDriveClientConfig GetConfig();
    bool IsConfigured();
}

