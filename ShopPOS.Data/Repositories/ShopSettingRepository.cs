using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Repositories;

/// <summary>
/// Data repository for ShopSetting operations.
/// </summary>
public class ShopSettingRepository : Repository<ShopSetting>, IShopSettingRepository
{
    public ShopSettingRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ShopSetting?> GetCurrentSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(cancellationToken);
    }
}

