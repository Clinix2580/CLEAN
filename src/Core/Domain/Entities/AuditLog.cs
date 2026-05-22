using OS.Domain.Common;

namespace OS.Domain.Entities;

/// <summary>
/// Represents an audit log entry for compliance and tracking purposes.
/// Captures all significant operations performed in the system.
/// </summary>
public class AuditLog : AggregateRoot
{
    /// <summary>
    /// Gets or sets the action performed (Create, Update, Delete, Login, Export, etc.).
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of entity affected by the action.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the entity that was affected.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Gets or sets the JSON representation of the entity's values before the change.
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// Gets or sets the JSON representation of the entity's values after the change.
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client that initiated the action.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the user agent string of the client browser or application.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets additional contextual information about the action.
    /// </summary>
    public string? AdditionalInfo { get; set; }

    /// <summary>
    /// Gets or sets the cryptographic hash of this audit log entry.
    /// Used to verify integrity of the audit trail.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hash of the previous audit log entry.
    /// Creates a chain of audit logs for immutability verification.
    /// </summary>
    public string PreviousHash { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new audit log entry.
    /// </summary>
    /// <param name="action">The action performed.</param>
    /// <param name="entityType">The type of entity affected.</param>
    /// <param name="userId">The ID of the user who performed the action.</param>
    public AuditLog(string action, string entityType, string userId)
    {
        Action = action;
        EntityType = entityType;
        CreatedBy = userId;
    }

    /// <summary>
    /// Initializes a new instance of the AuditLog class (parameterless for ORM).
    /// </summary>
    public AuditLog()
    {
    }

    /// <summary>
    /// Validates the integrity of this audit log using its hash.
    /// </summary>
    /// <param name="computedHash">The hash computed from the audit log data.</param>
    /// <returns>True if the computed hash matches the stored hash.</returns>
    public bool ValidateIntegrity(string computedHash)
    {
        return Hash == computedHash;
    }

    /// <summary>
    /// Gets a summary of the audit log for display purposes.
    /// </summary>
    public string GetSummary() =>
        $"{CreatedAt:yyyy-MM-dd HH:mm:ss} | User: {CreatedBy} | Action: {Action} | Entity: {EntityType}";
}
