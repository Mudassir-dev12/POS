using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Repositories;

/// <summary>
/// Data repository for AuditLog operations.
/// </summary>
public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<AuditLog>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}

