namespace OS.Domain.Entities;

/// <summary>
/// Rol del sistema con permisos específicos.
/// Define el nivel de acceso de un usuario a diferentes módulos.
/// </summary>
public class Role
{
    /// <summary>
    /// Identificador único del rol.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Código del rol (ej: SUPER_ADMIN, ADMIN, DOCTOR).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Nombre descriptivo del rol.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descripción del rol.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Usuarios asignados a este rol (navegación).
    /// </summary>
    public ICollection<UserRole> UserRoles { get; } = new List<UserRole>();

    /// <summary>
    /// Permisos asignados a este rol (navegación).
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; } = new List<RolePermission>();
}
