using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Core.Services;

/// <summary>
/// Domain service for accessing and modifying store settings.
/// </summary>
public class ShopSettingService : IShopSettingService
{
    private readonly IShopSettingRepository _repository;

    public ShopSettingService(IShopSettingRepository repository)
    {
        _repository = repository;
    }

    public async Task<ShopSetting> GetSettingsAsync()
    {
        var settings = await _repository.GetCurrentSettingsAsync();
        if (settings != null)
        {
            return settings;
        }

        // Return a default instance if none found in database
        var defaultSetting = new ShopSetting
        {
            ShopName = "Shop POS",
            CurrencySymbol = "Rs",
            ReceiptWidthMm = 80,
            DefaultTaxPercent = 0m,
            ReceiptFooter = "Thank you for shopping with us!"
        };

        return await _repository.AddAsync(defaultSetting);
    }

    public async Task<ShopSetting> UpdateSettingsAsync(ShopSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        if (setting.Id == 0)
        {
            return await _repository.AddAsync(setting);
        }

        await _repository.UpdateAsync(setting);
        return setting;
    }
}

