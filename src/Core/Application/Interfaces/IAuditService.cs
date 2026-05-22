using OS.Domain.Entities;

namespace OS.Application.Interfaces;

/// <summary>
/// Service interface for audit logging operations.
/// Provides methods to log entity changes and user actions for compliance and tracking.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs a system audit event when the caller does not yet provide an explicit user context.
    /// </summary>
    Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an audit event for an entity change.
    /// </summary>
    /// <param name="userId">The ID of the user who performed the action.</param>
    /// <param name="action">The action performed (Create, Update, Delete, etc.).</param>
    /// <param name="entityType">The type of entity being audited.</param>
    /// <param name="entityId">The ID of the entity being audited.</param>
    /// <param name="oldValues">JSON representation of old values (for updates).</param>
    /// <param name="newValues">JSON representation of new values.</param>
    /// <param name="ipAddress">The IP address of the client making the request.</param>
    /// <param name="userAgent">The user agent string of the client.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    Task LogAuditAsync(
        string userId,
        string action,
        string entityType,
        string? entityId,
        string? oldValues,
        string? newValues,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit logs for a specific entity.
    /// </summary>
    /// <param name="entityType">The type of entity to query.</param>
    /// <param name="entityId">The ID of the entity.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A collection of audit logs for the specified entity.</returns>
    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(
        string entityType,
        string? entityId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an informational audit event.
    /// </summary>
    Task LogInfoAsync(
        string entityType,
        string action,
        string? message = null,
        object? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a warning audit event.
    /// </summary>
    Task LogWarningAsync(
        string entityType,
        string action,
        string? message = null,
        object? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an error audit event.
    /// </summary>
    Task LogErrorAsync(
        string entityType,
        string action,
        string? message = null,
        object? userId = null,
        Exception? exception = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a critical audit event.
    /// </summary>
    Task LogCriticalAsync(
        string entityType,
        string action,
        string? message = null,
        object? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit logs for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user to query.</param>
    /// <param name="startDate">Start date for filtering logs (optional).</param>
    /// <param name="endDate">End date for filtering logs (optional).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A collection of audit logs for the specified user.</returns>
    Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent audit logs for dashboard reporting.
    /// </summary>
    /// <param name="count">Maximum number of audit logs to return.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A collection of the most recent audit logs.</returns>
    Task<IEnumerable<AuditLog>> GetRecentAuditLogsAsync(
        int count = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the integrity of an audit log using its hash.
    /// </summary>
    /// <param name="auditLog">The audit log to validate.</param>
    /// <returns>True if the audit log hash is valid, false otherwise.</returns>
    Task<bool> ValidateAuditLogIntegrityAsync(AuditLog auditLog);

    /// <summary>
    /// Purges old audit logs based on retention policy.
    /// </summary>
    /// <param name="daysToKeep">Number of days to retain audit logs.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The number of audit logs purged.</returns>
    Task<int> PurgeOldAuditLogsAsync(int daysToKeep, CancellationToken cancellationToken = default);
}
