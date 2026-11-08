using System;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ShopPOS.Data.Interceptors;

/// <summary>
/// EF Core command interceptor monitoring database execution times and flagging any queries
/// running synchronously on the UI thread or exceeding performance budgets (>100 ms).
/// </summary>
public class DbCommandExecutionWatchdogInterceptor : DbCommandInterceptor
{
    private const long SlowQueryThresholdMs = 100;
    private readonly ILogger<DbCommandExecutionWatchdogInterceptor> _logger;
    private readonly int _uiThreadId;

    public DbCommandExecutionWatchdogInterceptor(ILogger<DbCommandExecutionWatchdogInterceptor>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DbCommandExecutionWatchdogInterceptor>.Instance;
        // The main thread ID during UI initialization is typically 1
        _uiThreadId = 1;
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        InspectExecution(command, eventData.Duration);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        InspectExecution(command, eventData.Duration);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        InspectExecution(command, eventData.Duration);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        InspectExecution(command, eventData.Duration);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        InspectExecution(command, eventData.Duration);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        InspectExecution(command, eventData.Duration);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void InspectExecution(DbCommand command, TimeSpan duration)
    {
        bool isUiThread = Thread.CurrentThread.ManagedThreadId == _uiThreadId;
        bool isSlow = duration.TotalMilliseconds > SlowQueryThresholdMs;

        if (isUiThread && isSlow)
        {
            _logger.LogWarning(
                "[WATCHDOG] Slow database call on UI thread! Duration: {DurationMs:F1}ms. Command: {CommandText}",
                duration.TotalMilliseconds,
                TruncateCommand(command.CommandText));
        }
        else if (isSlow)
        {
            _logger.LogDebug(
                "[WATCHDOG] Slow query: {DurationMs:F1}ms. Command: {CommandText}",
                duration.TotalMilliseconds,
                TruncateCommand(command.CommandText));
        }
    }

    private static string TruncateCommand(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return string.Empty;
        var clean = sql.Replace(Environment.NewLine, " ").Trim();
        return clean.Length > 200 ? clean[..200] + "..." : clean;
    }
}

