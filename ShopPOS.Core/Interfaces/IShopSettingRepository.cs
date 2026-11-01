using ShopPOS.Core.Entities;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Repository for retrieving and persisting shop settings.
/// </summary>
public interface IShopSettingRepository : IRepository<ShopSetting>
{
    /// <summary>
    /// Retrieves the single active shop configuration record.
    /// </summary>
    Task<ShopSetting?> GetCurrentSettingsAsync(CancellationToken cancellationToken = default);
}

