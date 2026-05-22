using AutoMapper;
using OS.Application.DTOs;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Domain.Exceptions;
using IRuntimeAccessPolicy = OS.Domain.Interfaces.IRuntimeAccessPolicy;

namespace OS.Application.Services;

public class PatientService : IPatientService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly IRuntimeAccessPolicy _accessPolicy;

    public PatientService(IUnitOfWork unitOfWork, IMapper mapper, IAuditService auditService, IRuntimeAccessPolicy accessPolicy)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _auditService = auditService;
        _accessPolicy = accessPolicy;
    }

    public async Task<PatientDto?> GetByIdAsync(Guid id)
    {
        var patient = await _unitOfWork.Repository<Patient>().GetByIdAsync(id);
        if (patient == null) return null;

        await _auditService.LogAsync("READ", nameof(Patient), id.ToString(), null, "Patient detail accessed");
        var dto = _mapper.Map<PatientDto>(patient);
        dto.MedicalHistory = (await GetMedicalHistoryAsync(id)).ToList();
        return dto;
    }

    public async Task<IEnumerable<PatientListDto>> GetAllAsync()
    {
        var patients = await _unitOfWork.Repository<Patient>().GetAllAsync();
        await _auditService.LogAsync("READ_LIST", nameof(Patient), null, null, $"Patient list count: {patients.Count()}");
        return _mapper.Map<IEnumerable<PatientListDto>>(patients);
    }

    public async Task<PatientDto> CreateAsync(CreatePatientDto dto)
    {
        _accessPolicy.DemandWriteAccess();
        var patient = _mapper.Map<Patient>(dto);
        patient.Id = Guid.NewGuid();
        patient.CreatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<Patient>().AddAsync(patient);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("CREATE", nameof(Patient), patient.Id.ToString(), null, null);
        return _mapper.Map<PatientDto>(patient);
    }

    public async Task UpdateAsync(UpdatePatientDto dto)
    {
        _accessPolicy.DemandWriteAccess();
        var patient = await _unitOfWork.Repository<Patient>().GetByIdAsync(dto.Id)
            ?? throw new NotFoundException(nameof(Patient), dto.Id);

        var oldValues = System.Text.Json.JsonSerializer.Serialize(patient);
        _mapper.Map(dto, patient);
        patient.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<Patient>().UpdateAsync(patient);
        await _unitOfWork.SaveChangesAsync();

        var newValues = System.Text.Json.JsonSerializer.Serialize(patient);
        await _auditService.LogAsync("UPDATE", nameof(Patient), patient.Id.ToString(), oldValues, newValues);
    }

    public async Task DeleteAsync(Guid id)
    {
        _accessPolicy.DemandWriteAccess();
        var patient = await _unitOfWork.Repository<Patient>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Patient), id);

        await _unitOfWork.Repository<Patient>().DeleteAsync(patient);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("DELETE", nameof(Patient), id.ToString(), null, null);
    }

    public async Task BlockAsync(Guid id, string reason)
    {
        _accessPolicy.DemandWriteAccess();
        var patient = await _unitOfWork.Repository<Patient>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Patient), id);

        patient.IsBlocked = true;
        patient.BlockedAt = DateTime.UtcNow;
        patient.BlockReason = reason;
        patient.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<Patient>().UpdateAsync(patient);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("BLOCK", nameof(Patient), id.ToString(), null, reason);
    }

    public async Task UnblockAsync(Guid id)
    {
        _accessPolicy.DemandWriteAccess();
        var patient = await _unitOfWork.Repository<Patient>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Patient), id);

        patient.IsBlocked = false;
        patient.BlockedAt = null;
        patient.BlockReason = null;
        patient.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<Patient>().UpdateAsync(patient);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("UNBLOCK", nameof(Patient), id.ToString(), null, null);
    }

    public async Task<IEnumerable<MedicalRecordDto>> GetMedicalHistoryAsync(Guid patientId)
    {
        var records = await _unitOfWork.Repository<MedicalRecord>()
            .FindAsync(r => r.PatientId == patientId);
        await _auditService.LogAsync("READ", nameof(MedicalRecord), patientId.ToString(), null, "Medical history accessed");
        return _mapper.Map<IEnumerable<MedicalRecordDto>>(records.OrderByDescending(r => r.CreatedAt));
    }

    public async Task AddMedicalRecordAsync(Guid patientId, CreateMedicalRecordDto dto)
    {
        _accessPolicy.DemandWriteAccess();
        var record = _mapper.Map<MedicalRecord>(dto);
        record.Id = Guid.NewGuid();
        record.PatientId = patientId;
        record.CreatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<MedicalRecord>().AddAsync(record);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("CREATE", nameof(MedicalRecord), record.Id.ToString(), null, null);
    }
}
