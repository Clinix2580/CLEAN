namespace OS.Application.Interfaces;

/// <summary>
/// Unit of Work pattern interface for managing data persistence operations.
/// Provides access to repositories and manages transactions across the application.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Gets a generic repository for entities that do not yet have a dedicated port.
    /// This keeps legacy application services operational while bounded contexts are formalized.
    /// </summary>
    IRepository<TEntity> Repository<TEntity>() where TEntity : class;

    /// <summary>
    /// Gets the repository for User entities.
    /// </summary>
    IRepository<OS.Domain.Entities.User> Users { get; }

    /// <summary>
    /// Gets the repository for Patient entities.
    /// </summary>
    IRepository<OS.Domain.Entities.Patient> Patients { get; }

    /// <summary>
    /// Gets the repository for Product entities.
    /// </summary>
    IRepository<OS.Domain.Entities.Product> Products { get; }

    /// <summary>
    /// Gets the repository for AuditLog entities.
    /// </summary>
    IRepository<OS.Domain.Entities.AuditLog> AuditLogs { get; }

    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The number of entities affected by the save operation.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a transaction scope for the current operation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>An ITransaction object representing the transaction scope.</returns>
    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
