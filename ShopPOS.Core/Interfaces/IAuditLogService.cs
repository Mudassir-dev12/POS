using ShopPOS.Core.Entities;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Service responsible for recording system activity and security audit entries.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Writes a new audit log record asynchronously.
    /// </summary>
    /// <param name="userId">The acting user identifier, if known.</param>
    /// <param name="action">Action identifier (e.g. Login, Logout, PasswordChange).</param>
    /// <param name="entityName">Target entity type name, if applicable.</param>
    /// <param name="entityId">Target entity primary key, if applicable.</param>
    /// <param name="details">Human-readable details or change payload.</param>
    Task LogAsync(int? userId, string action, string? entityName = null, int? entityId = null, string? details = null);
}

