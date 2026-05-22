namespace OS.Domain.Entities;

/// <summary>
/// Pregunta de seguridad para recuperación de cuenta.
/// Cada usuario debe tener mínimo 3 preguntas de seguridad configuradas.
/// </summary>
public class SecurityQuestion
{
    /// <summary>
    /// Identificador único del registro de pregunta.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID del usuario al que pertenece esta pregunta.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Índice de la pregunta (1-3).
    /// Cada usuario debe tener preguntas con índices 1, 2 y 3.
    /// </summary>
    public int QuestionIndex { get; set; }

    /// <summary>
    /// Texto de la pregunta.
    /// Puede ser de un catálogo predefinido o texto libre.
    /// </summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// Hash de la respuesta (SHA-256 del texto en minúsculas).
    /// </summary>
    public string AnswerHash { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Usuario asociado (navegación).
    /// </summary>
    public User? User { get; set; }
}
