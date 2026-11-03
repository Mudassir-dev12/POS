using System;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShopPOS.Data.Interceptors;

/// <summary>
/// Intercepts connection opening events to enforce SQLite WAL mode and foreign key constraints,
/// and intercepts commands to provide backward-compatibility with older migration schemas.
/// </summary>
public class SqlitePragmaInterceptor : DbCommandInterceptor, IDbConnectionInterceptor
{
    private const string PragmaCommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";

    private static readonly Regex InsertRegex = new(
        @"INSERT\s+INTO\s+(?:""Products""|Products)\s*\((?<cols>[^)]+)\)\s*VALUES\s*\((?<vals>[^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SelectRegex = new(
        @"(?:""?[a-zA-Z0-9_]+""?\.)?""SearchText""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaCommandText;
        cmd.ExecuteNonQuery();
    }

    public async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaCommandText;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        SanitizeCommand(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        SanitizeCommand(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        SanitizeCommand(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SanitizeCommand(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void SanitizeCommand(DbCommand? command)
    {
        if (command == null || string.IsNullOrEmpty(command.CommandText)) return;
        if (!command.CommandText.Contains("SearchText", StringComparison.OrdinalIgnoreCase)) return;
        if (command.Connection == null) return;

        // Check if Products table has SearchText column in SQLite schema
        if (HasSearchTextColumn(command.Connection)) return;

        // Handle INSERT statements
        if (InsertRegex.IsMatch(command.CommandText))
        {
            command.CommandText = InsertRegex.Replace(command.CommandText, match =>
            {
                var cols = match.Groups["cols"].Value.Split(',').Select(c => c.Trim()).ToList();
                var vals = match.Groups["vals"].Value.Split(',').Select(v => v.Trim()).ToList();

                int idx = cols.FindIndex(c => c.Trim('"', '[', ']', '`', ' ').Equals("SearchText", StringComparison.OrdinalIgnoreCase));
                if (idx >= 0 && idx < vals.Count)
                {
                    string paramToRemove = vals[idx].Trim();
                    cols.RemoveAt(idx);
                    vals.RemoveAt(idx);

                    if (paramToRemove.StartsWith("@"))
                    {
                        string pName = paramToRemove.TrimStart('@');
                        if (command.Parameters.Contains(pName))
                        {
                            command.Parameters.RemoveAt(pName);
                        }
                        else if (command.Parameters.Contains(paramToRemove))
                        {
                            command.Parameters.RemoveAt(paramToRemove);
                        }
                    }

                    return $"INSERT INTO \"Products\" ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
                }

                return match.Value;
            });
        }
        else if (command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            command.CommandText = SelectRegex.Replace(command.CommandText, "'' AS \"SearchText\"");
        }
    }

    private static bool HasSearchTextColumn(DbConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(Products);";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "SearchText", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return true;
        }
    }
}
