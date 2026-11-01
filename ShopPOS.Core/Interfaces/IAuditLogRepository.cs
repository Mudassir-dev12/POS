using ShopPOS.Core.Entities;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Repository for recording and querying audit logs.
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    /// <summary>
    /// Gets the most recent audit log entries.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default);
}

