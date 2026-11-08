namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// OAuth client credentials for Google Drive API integration.
/// Retrieved from googledrive.client.json next to the application or environment variables.
/// </summary>
public class GoogleDriveClientConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);
}

