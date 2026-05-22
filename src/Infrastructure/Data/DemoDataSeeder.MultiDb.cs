using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OS.Infrastructure.Data;

internal static class DemoDataSeederMultiDb
{
    private const string SeedActor = "DEMO-SEED";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var clinicDb = scope.ServiceProvider.GetService<ClinicDbContext>();
        var auditDb = scope.ServiceProvider.GetService<AuditDbContext>();

        if (clinicDb == null)
            return; // nothing to seed

        // If patients already exist, assume seeded
        if (await clinicDb.Patients.AnyAsync(cancellationToken))
            return;

        var now = DateTime.UtcNow;

        var patients = new[]
        {
            new OS.Domain.Entities.Patient
            {
                Id = Guid.Parse("9b4e315c-d92a-4e98-9980-225c16d1a001"),
                FirstName = "Mariana",
                LastName = "Rivas Ortega",
                DateOfBirth = new DateTime(1988,4,12),
                MedicalRecordNumber = "DEMO-MX-0001",
                Phone = "+52 55 0100 0101",
                Email = "mariana.rivas.demo@example.invalid",
                Address = "Av. Clinica 101, Colonia Centro, Ciudad Demo",
                Notes = "Datos ficticios para demostracion.",
                CreatedAt = now.AddDays(-18),
                CreatedBy = SeedActor
            },
            new OS.Domain.Entities.Patient
            {
                Id = Guid.Parse("9b4e315c-d92a-4e98-9980-225c16d1a002"),
                FirstName = "Daniel",
                LastName = "Santos Vega",
                DateOfBirth = new DateTime(1976,9,3),
                MedicalRecordNumber = "DEMO-MX-0002",
                Phone = "+52 55 0100 0102",
                Email = "daniel.santos.demo@example.invalid",
                Address = "Calle Salud 22, Ciudad Demo",
                CreatedAt = now.AddDays(-16),
                CreatedBy = SeedActor
            }
        };

        clinicDb.Patients.AddRange(patients);

        var records = new[]
        {
            new OS.Domain.Entities.MedicalRecord
            {
                Id = Guid.Parse("f7b80df0-1fb0-48cc-8ec0-225c16d1b001"),
                PatientId = patients[0].Id,
                DoctorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                CreatedAt = now.AddDays(-14),
                Diagnosis = "Rinitis alergica estacional",
                Treatment = "Lavado nasal, antihistaminico no sedante",
                Prescription = "Loratadina 10 mg cada 24 horas por 7 dias.",
                DoctorName = "Dra. Andrea Torres"
            }
        };

        clinicDb.MedicalRecords.AddRange(records);

        var appointments = new[]
        {
            new OS.Domain.Entities.Appointment
            {
                Id = Guid.Parse("1963915b-6f01-47f2-b3d0-225c16d1c001"),
                PatientId = patients[0].Id,
                DoctorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                AppointmentDate = now.AddDays(3).Date.AddHours(10),
                Reason = "Seguimiento alergias",
                Status = OS.Domain.Entities.AppointmentStatus.Scheduled,
                CreatedAt = now.AddDays(-2)
            }
        };

        clinicDb.Appointments.AddRange(appointments);

        await clinicDb.SaveChangesAsync(cancellationToken);

        // Seed a light audit entry if audit DB is available
        if (auditDb != null)
        {
            // Calcular hash chain para el primer registro demo
            var previousHash = await auditDb.AuditLogs
                .OrderByDescending(a => a.ChangedAtUtc)
                .ThenByDescending(a => a.Id)
                .Select(a => a.VerificationHash)
                .FirstOrDefaultAsync(cancellationToken);

            var audit = new AuditLogEntry
            {
                TableName = "patients",
                Operation = "INSERT",
                UserId = SeedActor,
                RecordId = patients[0].Id.ToString(),
                NewValues = "{\"firstName\":\"Mariana\"}",
                ChangedAtUtc = DateTime.UtcNow,
                PreviousHash = string.IsNullOrWhiteSpace(previousHash) ? null : previousHash
            };

            auditDb.AuditLogs.Add(audit);
            await auditDb.SaveChangesAsync(cancellationToken);

            // Calcular y persistir el VerificationHash con el Id real asignado
            var hashInput = new System.Text.StringBuilder();
            hashInput.Append(audit.Id.ToString()).Append('|');
            hashInput.Append(audit.TableName).Append('|');
            hashInput.Append(audit.Operation).Append('|');
            hashInput.Append(audit.RecordId ?? string.Empty).Append('|');
            hashInput.Append(audit.UserId).Append('|');
            hashInput.Append(audit.OldValues ?? string.Empty).Append('|');
            hashInput.Append(audit.NewValues ?? string.Empty).Append('|');
            hashInput.Append(audit.ChangedAtUtc.ToString("O")).Append('|');
            hashInput.Append(audit.PreviousHash ?? string.Empty).Append('|');
            hashInput.Append(audit.IpAddress ?? string.Empty);

            var hashBytes = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(hashInput.ToString()));
            audit.VerificationHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            await auditDb.SaveChangesAsync(cancellationToken);
        }
    }
}
