using System.Threading;
using System.Threading.Tasks;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Secure storage contract for the GitHub Personal Access Token.
/// Uses Windows DPAPI (ProtectedData) to store encrypted at rest.
/// </summary>
public interface IGitHubTokenStore
{
    Task SaveTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
    Task<bool> HasTokenAsync(CancellationToken cancellationToken = default);
    Task DeleteTokenAsync(CancellationToken cancellationToken = default);
}

