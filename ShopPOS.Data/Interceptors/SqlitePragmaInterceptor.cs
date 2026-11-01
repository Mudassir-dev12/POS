using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShopPOS.Data.Interceptors;

/// <summary>
/// Intercepts connection opening events to enforce SQLite WAL mode and foreign key constraints.
/// </summary>
public class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string PragmaCommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaCommandText;
        cmd.ExecuteNonQuery();
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaCommandText;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}

