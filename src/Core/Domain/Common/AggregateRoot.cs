namespace OS.Domain.Common;

/// <summary>
/// Base class for all aggregate roots in the domain layer.
/// Provides common properties and behavior for all entities.
/// </summary>
public abstract class AggregateRoot
{
    /// <summary>
    /// Gets or sets the unique identifier for the entity.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the date and time when the entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the ID of the user who created the entity.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the entity was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who last updated the entity.
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entity is deleted (soft delete).
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the entity was deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Marks the entity as deleted (soft delete).
    /// </summary>
    /// <param name="deletedBy">The ID of the user who deleted the entity.</param>
    public virtual void MarkAsDeleted(string deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedBy = deletedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Restores a soft-deleted entity.
    /// </summary>
    /// <param name="restoredBy">The ID of the user who restored the entity.</param>
    public virtual void Restore(string restoredBy)
    {
        IsDeleted = false;
        DeletedAt = null;
        UpdatedBy = restoredBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
