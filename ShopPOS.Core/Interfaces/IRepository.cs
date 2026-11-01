using System.Linq.Expressions;
using ShopPOS.Core.Common;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Generic repository interface for asynchronous data access.
/// </summary>
/// <typeparam name="T">BaseEntity derivative.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>
    /// Gets an entity by its primary key.
    /// </summary>
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active non-deleted entities.
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds entities matching a predicate condition.
    /// </summary>
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an entity as soft deleted.
    /// </summary>
    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
}

