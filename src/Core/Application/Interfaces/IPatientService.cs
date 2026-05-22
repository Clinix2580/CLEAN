using OS.Application.DTOs;

namespace OS.Application.Interfaces;

public interface IPatientService
{
    Task<PatientDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<PatientListDto>> GetAllAsync();
    Task<PatientDto> CreateAsync(CreatePatientDto dto);
    Task UpdateAsync(UpdatePatientDto dto);
    Task DeleteAsync(Guid id);
    Task BlockAsync(Guid id, string reason);
    Task UnblockAsync(Guid id);
    Task<IEnumerable<MedicalRecordDto>> GetMedicalHistoryAsync(Guid patientId);
    Task AddMedicalRecordAsync(Guid patientId, CreateMedicalRecordDto dto);
}
