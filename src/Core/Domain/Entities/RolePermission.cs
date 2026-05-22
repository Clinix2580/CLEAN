namespace OS.Domain.Entities;

/// <summary>
/// Asignación de permiso a un rol.
/// Tabla de unión muchos-a-muchos entre Role y Permission.
/// </summary>
public class RolePermission
{
    /// <summary>
    /// Identificador único de la asignación.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID del rol.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// ID del permiso.
    /// </summary>
    public Guid PermissionId { get; set; }

    /// <summary>
    /// Fecha y hora de asignación.
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// ID del usuario admin que realizó la asignación.
    /// </summary>
    public Guid? AssignedBy { get; set; }

    /// <summary>
    /// Rol asociado (navegación).
    /// </summary>
    public Role Role { get; set; } = null!;

    /// <summary>
    /// Permiso asociado (navegación).
    /// </summary>
    public Permission Permission { get; set; } = null!;
}
