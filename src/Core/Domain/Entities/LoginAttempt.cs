namespace OS.Domain.Entities;

/// <summary>
/// Registro de intentos de login de un usuario.
/// Se utiliza para implementar bloqueo por intentos fallidos y backoff exponencial.
/// </summary>
public class LoginAttempt
{
    /// <summary>
    /// Identificador único del registro de intento.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nombre de usuario (no el ID, para permitir tracking antes de autenticación).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Hash del fingerprint de la máquina (HWID).
    /// </summary>
    public string MachineFingerprintHash { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora del intento.
    /// </summary>
    public DateTime AttemptAt { get; set; }

    /// <summary>
    /// Indica si el intento fue exitoso.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Dirección IP del cliente (si está disponible).
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User Agent del cliente (si está disponible).
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Razón del fallo (si el intento falló).
    /// </summary>
    public string? FailureReason { get; set; }
}
