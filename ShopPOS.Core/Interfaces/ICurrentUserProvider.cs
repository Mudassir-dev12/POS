using ShopPOS.Core.Entities;

namespace ShopPOS.Core.Interfaces;

public interface ICurrentUserProvider
{
    int UserId { get; }
    string UserName { get; }
    string FullName { get; }
    User GetCurrentUser();
}

