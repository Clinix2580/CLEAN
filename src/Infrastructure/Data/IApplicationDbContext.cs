using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OS.Domain.Entities;

namespace OS.Infrastructure.Data;

/// <summary>
/// Contrato para el contexto de datos de la aplicación.
/// Define los DbSets para todas las entidades del dominio.
/// </summary>
public interface IApplicationDbContext : IAsyncDisposable
{
    DbSet<User> Users { get; }
    DbSet<Patient> Patients { get; }
    DbSet<Product> Products { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<MedicalRecord> MedicalRecords { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<ConsentRecord> ConsentRecords { get; }
    DbSet<ArcoRequest> ArcoRequests { get; }

    DbSet<TEntity> Set<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TEntity>() where TEntity : class;
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
