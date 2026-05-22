namespace OS.Application.Interfaces;

/// <summary>
/// Generic repository interface for CRUD operations on aggregate roots.
/// Provides abstraction for data persistence operations without exposing
/// implementation details to the application layer.
/// </summary>
/// <typeparam name="TEntity">The type of entity this repository manages.</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Gets an entity by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The entity if found, null otherwise.</returns>
    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities of the type.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A collection of all entities.</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds entities matching the given predicate.
    /// </summary>
    /// <param name="predicate">The condition to filter entities.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A collection of entities matching the predicate.</returns>
    Task<IEnumerable<TEntity>> FindAsync(
        System.Linq.Expressions.Expression<System.Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <returns>The added entity.</returns>
    Task<TEntity> AddAsync(TEntity entity);

    /// <summary>
    /// Adds multiple entities to the repository.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <returns>The added entities.</returns>
    Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    Task UpdateAsync(TEntity entity);

    /// <summary>
    /// Removes an entity from the repository.
    /// </summary>
    /// <param name="entity">The entity to remove.</param>
    Task RemoveAsync(TEntity entity);

    /// <summary>
    /// Removes an entity from the repository.
    /// Compatibility alias for legacy application services.
    /// </summary>
    Task DeleteAsync(TEntity entity);

    /// <summary>
    /// Removes multiple entities from the repository.
    /// </summary>
    /// <param name="entities">The entities to remove.</param>
    Task RemoveRangeAsync(IEnumerable<TEntity> entities);

    /// <summary>
    /// Checks if an entity exists matching the given predicate.
    /// </summary>
    /// <param name="predicate">The condition to check.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>True if an entity matches the predicate, false otherwise.</returns>
    Task<bool> ExistsAsync(
        System.Linq.Expressions.Expression<System.Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entities matching the given predicate.
    /// </summary>
    /// <param name="predicate">The condition to filter entities (optional).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The count of matching entities.</returns>
    Task<int> CountAsync(
        System.Linq.Expressions.Expression<System.Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);
}
