namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Secure storage contract for Google OAuth tokens using Windows DPAPI.
/// </summary>
public interface IDriveTokenStore
{
    Task SaveTokenAsync(string tokenJson, CancellationToken cancellationToken = default);
    Task<string?> LoadTokenAsync(CancellationToken cancellationToken = default);
    Task DeleteTokenAsync(CancellationToken cancellationToken = default);
    bool HasToken();
}

