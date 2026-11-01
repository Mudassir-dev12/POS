namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Executes multi-operation business workflows inside an atomic database transaction.
/// </summary>
public interface ITransactionRunner
{
    Task<T> RunInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task RunInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
}

