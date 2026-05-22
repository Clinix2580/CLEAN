namespace OS.Application.DTOs;

public class MedicalRecordDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string? Treatment { get; set; }
    public string? Prescription { get; set; }
    public string? Doctor { get; set; }
}

public class CreateMedicalRecordDto
{
    public DateTime Date { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string? Treatment { get; set; }
    public string? Prescription { get; set; }
    public string? Doctor { get; set; }
}
