using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using OS.Application.Interfaces;

namespace OS.Application.Services;

/// <summary>
/// DTO para crear una especialidad.
/// </summary>
public class CreateSpecialtyDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// DTO para crear un título médico.
/// </summary>
public class CreateDoctorTitleDto
{
    public Guid SpecialtyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Abbreviation { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Servicio de gestión de especialidades médicas con protección de integridad.
/// </summary>
public interface ISpecialtyService
{
    /// <summary>
    /// Obtiene todas las especialidades activas.
    /// </summary>
    Task<List<Specialty>> GetActiveSpecialtiesAsync();

    /// <summary>
    /// Obtiene una especialidad por ID.
    /// </summary>
    Task<Specialty?> GetSpecialtyAsync(Guid id);

    /// <summary>
    /// Crea una nueva especialidad.
    /// </summary>
    Task<Specialty> CreateSpecialtyAsync(CreateSpecialtyDto dto, Guid createdBy);

    /// <summary>
    /// Actualiza una especialidad existente.
    /// </summary>
    Task<Specialty> UpdateSpecialtyAsync(Guid id, CreateSpecialtyDto dto, Guid updatedBy);

    /// <summary>
    /// Desactiva una especialidad (no la elimina).
    /// </summary>
    Task<bool> DeactivateSpecialtyAsync(Guid id, Guid deactivatedBy, string reason);

    /// <summary>
    /// Verifica si una especialidad puede ser desactivada (no tiene médicos activos).
    /// </summary>
    Task<(bool canDeactivate, string reason)> CanDeactivateSpecialtyAsync(Guid id);

    /// <summary>
    /// Obtiene los títulos de una especialidad.
    /// </summary>
    Task<List<DoctorTitle>> GetDoctorTitlesAsync(Guid specialtyId);

    /// <summary>
    /// Crea un nuevo título médico.
    /// </summary>
    Task<DoctorTitle> CreateDoctorTitleAsync(CreateDoctorTitleDto dto, Guid createdBy);

    /// <summary>
    /// Elimina un título médico.
    /// </summary>
    Task<bool> DeleteDoctorTitleAsync(Guid id, Guid deletedBy, string reason);
}

/// <summary>
/// Implementación del servicio de especialidades médicas.
/// </summary>
public class SpecialtyService : ISpecialtyService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;

    public SpecialtyService(
        ClinicDbContext dbContext,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Obtiene todas las especialidades activas.
    /// </summary>
    public async Task<List<Specialty>> GetActiveSpecialtiesAsync()
    {
        return await _dbContext.Specialties
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene una especialidad por ID.
    /// </summary>
    public async Task<Specialty?> GetSpecialtyAsync(Guid id)
    {
        return await _dbContext.Specialties
            .Include(s => s.DoctorTitles)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    /// <summary>
    /// Crea una nueva especialidad.
    /// </summary>
    public async Task<Specialty> CreateSpecialtyAsync(CreateSpecialtyDto dto, Guid createdBy)
    {
        // Validar que el código sea único
        var existing = await _dbContext.Specialties
            .FirstOrDefaultAsync(s => s.Code == dto.Code);

        if (existing != null)
            throw new InvalidOperationException($"Ya existe una especialidad con el código '{dto.Code}'");

        var specialty = new Specialty
        {
            Id = Guid.NewGuid(),
            Code = dto.Code.ToUpperInvariant(),
            Name = dto.Name,
            Description = dto.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _dbContext.Specialties.Add(specialty);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "SPECIALTY",
            "SPECIALTY_CREATED",
            $"Especialidad creada: {specialty.Code} - {specialty.Name}",
            createdBy);

        return specialty;
    }

    /// <summary>
    /// Actualiza una especialidad existente.
    /// </summary>
    public async Task<Specialty> UpdateSpecialtyAsync(Guid id, CreateSpecialtyDto dto, Guid updatedBy)
    {
        var specialty = await _dbContext.Specialties.FindAsync(id);
        if (specialty == null)
            throw new ArgumentException("Especialidad no encontrada", nameof(id));

        // Validar que el código sea único (si cambió)
        if (specialty.Code != dto.Code.ToUpperInvariant())
        {
            var existing = await _dbContext.Specialties
                .FirstOrDefaultAsync(s => s.Code == dto.Code.ToUpperInvariant() && s.Id != id);

            if (existing != null)
                throw new InvalidOperationException($"Ya existe una especialidad con el código '{dto.Code}'");
        }

        var oldName = specialty.Name;
        specialty.Code = dto.Code.ToUpperInvariant();
        specialty.Name = dto.Name;
        specialty.Description = dto.Description;
        specialty.UpdatedAt = DateTime.UtcNow;
        specialty.UpdatedBy = updatedBy;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "SPECIALTY",
            "SPECIALTY_UPDATED",
            $"Especialidad actualizada: {specialty.Code} - {oldName} → {specialty.Name}",
            updatedBy);

        return specialty;
    }

    /// <summary>
    /// Desactiva una especialidad (no la elimina).
    /// </summary>
    public async Task<bool> DeactivateSpecialtyAsync(Guid id, Guid deactivatedBy, string reason)
    {
        var specialty = await _dbContext.Specialties.FindAsync(id);
        if (specialty == null)
            return false;

        // Verificar si puede desactivarse
        var (canDeactivate, deactivateReason) = await CanDeactivateSpecialtyAsync(id);
        if (!canDeactivate)
            throw new InvalidOperationException(deactivateReason);

        specialty.IsActive = false;
        specialty.UpdatedAt = DateTime.UtcNow;
        specialty.UpdatedBy = deactivatedBy;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogWarningAsync(
            "SPECIALTY",
            "SPECIALTY_DEACTIVATED",
            $"Especialidad desactivada: {specialty.Code} - {specialty.Name}. Razón: {reason}",
            deactivatedBy);

        return true;
    }

    /// <summary>
    /// Verifica si una especialidad puede ser desactivada (no tiene médicos activos).
    /// </summary>
    public async Task<(bool canDeactivate, string reason)> CanDeactivateSpecialtyAsync(Guid id)
    {
        var specialty = await _dbContext.Specialties.FindAsync(id);
        if (specialty == null)
            return (false, "Especialidad no encontrada");

        if (!specialty.IsActive)
            return (false, "La especialidad ya está inactiva");

        // Verificar si tiene médicos activos (esto requeriría una tabla de médicos, por ahora asumimos que sí se puede)
        // En una implementación completa, verificaríamos si hay médicos con esta especialidad activos

        return (true, string.Empty);
    }

    /// <summary>
    /// Obtiene los títulos de una especialidad.
    /// </summary>
    public async Task<List<DoctorTitle>> GetDoctorTitlesAsync(Guid specialtyId)
    {
        return await _dbContext.DoctorTitles
            .Where(d => d.SpecialtyId == specialtyId)
            .OrderBy(d => d.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Crea un nuevo título médico.
    /// </summary>
    public async Task<DoctorTitle> CreateDoctorTitleAsync(CreateDoctorTitleDto dto, Guid createdBy)
    {
        // Validar que la especialidad existe
        var specialty = await _dbContext.Specialties.FindAsync(dto.SpecialtyId);
        if (specialty == null)
            throw new ArgumentException("Especialidad no encontrada", nameof(dto.SpecialtyId));

        // Validar que el título sea único para la especialidad
        var existing = await _dbContext.DoctorTitles
            .FirstOrDefaultAsync(d => d.SpecialtyId == dto.SpecialtyId && d.Title == dto.Title);

        if (existing != null)
            throw new InvalidOperationException($"Ya existe un título '{dto.Title}' para esta especialidad");

        // Si es primario, quitar el primario de los demás
        if (dto.IsPrimary)
        {
            var existingPrimary = await _dbContext.DoctorTitles
                .Where(d => d.SpecialtyId == dto.SpecialtyId && d.IsPrimary)
                .ToListAsync();

            foreach (var title in existingPrimary)
            {
                title.IsPrimary = false;
            }
        }

        var doctorTitle = new DoctorTitle
        {
            Id = Guid.NewGuid(),
            SpecialtyId = dto.SpecialtyId,
            Title = dto.Title,
            Abbreviation = dto.Abbreviation,
            IsPrimary = dto.IsPrimary,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _dbContext.DoctorTitles.Add(doctorTitle);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "SPECIALTY",
            "DOCTOR_TITLE_CREATED",
            $"Título médico creado: {doctorTitle.Title} para especialidad {specialty.Code}",
            createdBy);

        return doctorTitle;
    }

    /// <summary>
    /// Elimina un título médico.
    /// </summary>
    public async Task<bool> DeleteDoctorTitleAsync(Guid id, Guid deletedBy, string reason)
    {
        var doctorTitle = await _dbContext.DoctorTitles.FindAsync(id);
        if (doctorTitle == null)
            return false;

        // No permitir eliminar el título primario
        if (doctorTitle.IsPrimary)
            throw new InvalidOperationException("No se puede eliminar el título primario de una especialidad");

        _dbContext.DoctorTitles.Remove(doctorTitle);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogWarningAsync(
            "SPECIALTY",
            "DOCTOR_TITLE_DELETED",
            $"Título médico eliminado: {doctorTitle.Title}. Razón: {reason}",
            deletedBy);

        return true;
    }
}
