namespace OS.Domain.Entities;

/// <summary>
/// Historial de contraseñas de un usuario.
/// Almacena las últimas 5 contraseñas hasheadas para prevenir reutilización.
/// </summary>
public class PasswordHistory
{
    /// <summary>
    /// Identificador único del registro de historial.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID del usuario al que pertenece este historial.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Hash de la contraseña (SHA-256 + PBKDF2).
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora en que se cambió la contraseña.
    /// </summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>
    /// Usuario asociado (navegación).
    /// </summary>
    public User? User { get; set; }
}
