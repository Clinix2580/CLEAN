namespace OS.Application.Interfaces;

/// <summary>
/// Represents a transaction scope for database operations.
/// Ensures that multiple operations are atomic and consistent.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
