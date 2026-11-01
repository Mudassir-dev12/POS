using ShopPOS.Core.Entities;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Provides user identity and session access for the single-user application.
/// </summary>
public interface IAuthService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    Task LogoutAsync();
}
