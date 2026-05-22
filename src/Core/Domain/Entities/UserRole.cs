namespace OS.Domain.Entities;

/// <summary>
/// Asignación de rol a un usuario.
/// Tabla de unión muchos-a-muchos entre User y Role.
/// </summary>
public class UserRole
{
    /// <summary>
    /// Identificador único de la asignación.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID del usuario.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// ID del rol.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Fecha y hora de asignación.
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// ID del usuario admin que realizó la asignación.
    /// </summary>
    public Guid? AssignedBy { get; set; }

    /// <summary>
    /// Fecha y hora de revocación (si aplica).
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Razón de revocación (si aplica).
    /// </summary>
    public string? RevocationReason { get; set; }

    /// <summary>
    /// Usuario asociado (navegación).
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Rol asociado (navegación).
    /// </summary>
    public Role Role { get; set; } = null!;
}
