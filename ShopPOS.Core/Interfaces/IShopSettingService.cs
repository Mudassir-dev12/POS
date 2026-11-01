using ShopPOS.Core.Entities;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Provides access and updates to shop configuration settings.
/// </summary>
public interface IShopSettingService
{
    /// <summary>
    /// Retrieves the current active shop settings.
    /// </summary>
    Task<ShopSetting> GetSettingsAsync();

    /// <summary>
    /// Updates and persists the shop settings.
    /// </summary>
    Task<ShopSetting> UpdateSettingsAsync(ShopSetting setting);
}

