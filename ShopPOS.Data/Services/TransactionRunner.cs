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
        // If already participating in an active transaction, execute action directly within ambient transaction
        if (_context.Database.CurrentTransaction != null)
        {
            try
            {
                var result = await action();
                if (result is ShopPOS.Core.Models.Result r && !r.Success)
                {
                    _context.ChangeTracker.Clear();
                }
                return result;
            }
            catch
            {
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action();
            if (result is ShopPOS.Core.Models.Result r && !r.Success)
            {
                await tx.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                return result;
            }

            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(cancellationToken);
            }
            catch { }
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task RunInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        // If already participating in an active transaction, execute action directly within ambient transaction
        if (_context.Database.CurrentTransaction != null)
        {
            try
            {
                await action();
                return;
            }
            catch
            {
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action();
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(cancellationToken);
            }
            catch { }
            _context.ChangeTracker.Clear();
            throw;
        }
    }
}

