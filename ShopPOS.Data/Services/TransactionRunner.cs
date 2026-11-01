using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

public class TransactionRunner : ITransactionRunner
{
    private readonly AppDbContext _context;

    public TransactionRunner(AppDbContext context)
    {
        _context = context;
    }

    public async Task<T> RunInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action();
            if (result is ShopPOS.Core.Models.Result r && !r.Success)
            {
                await tx.RollbackAsync(cancellationToken);
                return result;
            }

            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RunInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action();
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

