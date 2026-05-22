namespace OS.Domain.Entities;

/// <summary>
/// Permiso individual del sistema.
/// Representa una acción específica en un módulo.
/// </summary>
public class Permission
{
    /// <summary>
    /// Identificador único del permiso.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Código del permiso (ej: PATIENTS_READ, PATIENTS_WRITE).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Nombre descriptivo del permiso.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descripción del permiso.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Módulo al que pertenece el permiso.
    /// </summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Nivel de permiso (READ, WRITE, DELETE).
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Roles que tienen este permiso (navegación).
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; } = new List<RolePermission>();
}
