namespace OS.Domain.Entities;

/// <summary>
/// Especialidad médica inalterable.
/// </summary>
public class Specialty
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Código único de la especialidad (ej: CARDIO, NEURO, PEDIA).
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Nombre de la especialidad.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Descripción de la especialidad.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Indica si la especialidad está activa.
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Fecha de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// ID del usuario que creó la especialidad.
    /// </summary>
    public Guid CreatedBy { get; set; }
    
    /// <summary>
    /// Fecha de última actualización.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// ID del usuario que actualizó la especialidad.
    /// </summary>
    public Guid? UpdatedBy { get; set; }
    
    // Navegación
    public User? Creator { get; set; }
    public User? Updater { get; set; }
    public ICollection<DoctorTitle> DoctorTitles { get; } = new List<DoctorTitle>();
}

/// <summary>
/// Título médico asociado a una especialidad.
/// </summary>
public class DoctorTitle
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID de la especialidad.
    /// </summary>
    public Guid SpecialtyId { get; set; }
    
    /// <summary>
    /// Título completo (ej: Cardiólogo Clínico, Neurólogo Intervencionista).
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Abreviatura del título (ej: Dr. Card., Dr. Neuro).
    /// </summary>
    public string? Abbreviation { get; set; }
    
    /// <summary>
    /// Indica si es el título principal de la especialidad.
    /// </summary>
    public bool IsPrimary { get; set; }
    
    /// <summary>
    /// Fecha de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// ID del usuario que creó el título.
    /// </summary>
    public Guid CreatedBy { get; set; }
    
    // Navegación
    public Specialty? Specialty { get; set; }
    public User? Creator { get; set; }
}
