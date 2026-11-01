using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Core.Services;

public class CurrentUserProvider : ICurrentUserProvider
{
    public int UserId => 1;
    public string UserName => "owner";
    public string FullName => "Owner";

    private static readonly User _ownerUser = new()
    {
        Id = 1,
        Username = "owner",
        FullName = "Owner",
        IsActive = true,
        RoleId = 1,
        PasswordHash = string.Empty,
        MustChangePassword = false
    };

    public User GetCurrentUser() => _ownerUser;
}
