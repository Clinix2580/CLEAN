using OS.Domain.Entities;
using System;
using System.Collections.Generic;

namespace OS.Infrastructure.Data.Seeders;

/// <summary>
/// Datos de demostración ficticios para modo demo con solo lectura.
/// Proporciona pacientes, usuarios, servicios y transacciones realistas para pruebas.
/// </summary>
public static class DemoDataSeeder
{
    public static List<Patient> GetDemoPatients() => new()
    {
        new Patient
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000001"),
            FirstName = "Carlos",
            LastName = "Gómez López",
            DateOfBirth = new DateTime(1980, 5, 15),
            Gender = "M",
            Email = "carlos.gomez@email.com",
            Phone = "+1-555-0001",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow
        },
        new Patient
        {
            Id = Guid.Parse("00000002-0000-0000-0000-000000000002"),
            FirstName = "María",
            LastName = "Rodríguez Martínez",
            DateOfBirth = new DateTime(1992, 3, 22),
            Gender = "F",
            Email = "maria.rodriguez@email.com",
            Phone = "+1-555-0002",
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            UpdatedAt = DateTime.UtcNow
        },
        new Patient
        {
            Id = Guid.Parse("00000003-0000-0000-0000-000000000003"),
            FirstName = "Juan",
            LastName = "Hernández Silva",
            DateOfBirth = new DateTime(1975, 11, 8),
            Gender = "M",
            Email = "juan.hernandez@email.com",
            Phone = "+1-555-0003",
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            UpdatedAt = DateTime.UtcNow
        },
        new Patient
        {
            Id = Guid.Parse("00000004-0000-0000-0000-000000000004"),
            FirstName = "Laura",
            LastName = "Fernández García",
            DateOfBirth = new DateTime(1988, 7, 19),
            Gender = "F",
            Email = "laura.fernandez@email.com",
            Phone = "+1-555-0004",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow
        },
        new Patient
        {
            Id = Guid.Parse("00000005-0000-0000-0000-000000000005"),
            FirstName = "Antonio",
            LastName = "López Sánchez",
            DateOfBirth = new DateTime(1965, 2, 14),
            Gender = "M",
            Email = "antonio.lopez@email.com",
            Phone = "+1-555-0005",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow
        }
    };

    public static List<User> GetDemoUsers() => new()
    {
        new User
        {
            Id = Guid.Parse("10000001-0000-0000-0000-000000000001"),
            Username = "demo.doctor@clinic.local",
            Email = "demo.doctor@clinic.local",
            FullName = "Dr. Demo Physician",
            Role = "Doctor",
            CreatedAt = DateTime.UtcNow.AddDays(-90),
            UpdatedAt = DateTime.UtcNow
        },
        new User
        {
            Id = Guid.Parse("10000002-0000-0000-0000-000000000002"),
            Username = "demo.nurse@clinic.local",
            Email = "demo.nurse@clinic.local",
            FullName = "Demo Nurse Staff",
            Role = "Nurse",
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            UpdatedAt = DateTime.UtcNow
        },
        new User
        {
            Id = Guid.Parse("10000003-0000-0000-0000-000000000003"),
            Username = "demo.admin@clinic.local",
            Email = "demo.admin@clinic.local",
            FullName = "Admin Demo User",
            Role = "Administrator",
            CreatedAt = DateTime.UtcNow.AddDays(-90),
            UpdatedAt = DateTime.UtcNow
        }
    };

    public static List<Dictionary<string, object>> GetDemoAppointments() => new()
    {
        new Dictionary<string, object>
        {
            { "Id", Guid.Parse("20000001-0000-0000-0000-000000000001") },
            { "PatientId", Guid.Parse("00000001-0000-0000-0000-000000000001") },
            { "DoctorId", Guid.Parse("10000001-0000-0000-0000-000000000001") },
            { "AppointmentDate", DateTime.Now.AddDays(3) },
            { "ReasonForVisit", "Checkup General" },
            { "Status", "Scheduled" }
        },
        new Dictionary<string, object>
        {
            { "Id", Guid.Parse("20000002-0000-0000-0000-000000000002") },
            { "PatientId", Guid.Parse("00000002-0000-0000-0000-000000000002") },
            { "DoctorId", Guid.Parse("10000001-0000-0000-0000-000000000001") },
            { "AppointmentDate", DateTime.Now.AddDays(5) },
            { "ReasonForVisit", "Seguimiento Post-Cirugía" },
            { "Status", "Scheduled" }
        }
    };
}
