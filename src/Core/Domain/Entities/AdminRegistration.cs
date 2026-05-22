namespace OS.Domain.Entities;

/// <summary>
/// Nivel de membresía del plan de licenciamiento.
/// </summary>
public enum MembershipLevel
{
    Level1, // Consultorio - 3 HWID
    Level2, // Clínica - 15 HWID
    Level3, // Red Clínica - 40 HWID
    Level4  // Empresarial - Ilimitado
}

/// <summary>
/// Registro de administrador con su plan de licenciamiento.
/// </summary>
public class AdminRegistration
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Usuario administrador vinculado.
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Nivel de membresía del plan.
    /// </summary>
    public MembershipLevel MembershipLevel { get; set; }
    
    /// <summary>
    /// Límite máximo de HWID autorizados según el plan.
    /// </summary>
    public int MaxHwidLimit { get; set; }
    
    /// <summary>
    /// Nombre de la organización.
    /// </summary>
    public string OrganizationName { get; set; } = string.Empty;
    
    /// <summary>
    /// Email de contacto del administrador.
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Fecha de inicio del plan.
    /// </summary>
    public DateTime PlanStartDate { get; set; }
    
    /// <summary>
    /// Fecha de fin del plan (null para planes permanentes).
    /// </summary>
    public DateTime? PlanEndDate { get; set; }
    
    /// <summary>
    /// Indica si el plan está activo.
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Fecha de creación del registro.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Fecha de última actualización.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
    
    // Navegación
    public User? User { get; set; }
    public ICollection<LicenseDistribution> LicenseDistributions { get; } = new List<LicenseDistribution>();
    public ICollection<LicenseRequest> LicenseRequests { get; } = new List<LicenseRequest>();
}
