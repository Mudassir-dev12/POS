using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Core.Services;

/// <summary>
/// Single-user implementation of IAuthService providing the local Owner context.
/// </summary>
public class AuthService : IAuthService
{
    private readonly ICurrentUserProvider _userProvider;

    public AuthService(ICurrentUserProvider userProvider)
    {
        _userProvider = userProvider;
    }

    public User? CurrentUser => _userProvider.GetCurrentUser();

    public bool IsAuthenticated => true;

    public Task LogoutAsync() => Task.CompletedTask;
}
