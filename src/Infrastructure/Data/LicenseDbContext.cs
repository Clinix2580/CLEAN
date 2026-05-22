using Microsoft.EntityFrameworkCore;

namespace OS.Infrastructure.Data;

/// <summary>
/// DbContext para el dominio de licencias.
/// Almacena: Licencias emitidas, historial, bindings de HWID, eventos.
/// Base de datos: licenses_db
/// Acceso: Append-only para tabla de licencias emitidas
/// </summary>
public sealed class LicenseDbContext : DbContext
{
    public DbSet<IssuedLicense> IssuedLicenses { get; set; } = null!;
    public DbSet<LicenseEvent> LicenseEvents { get; set; } = null!;
    public DbSet<HardwareBinding> HardwareBindings { get; set; } = null!;

#pragma warning disable IL2026, IL3050 // EF Core DbContext construction is isolated to the offline license persistence boundary.
    public LicenseDbContext(DbContextOptions<LicenseDbContext> options)
        : base(options)
    {
    }
#pragma warning restore IL2026, IL3050

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de IssuedLicenses (Append-only)
        modelBuilder.Entity<IssuedLicense>(entity =>
        {
            entity.ToTable("issued_licenses");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(l => l.LicenseId).HasColumnName("license_id").IsRequired().HasMaxLength(50);
            entity.Property(l => l.MachineFingerprintHash).HasColumnName("machine_fingerprint_hash").IsRequired().HasMaxLength(64);
            entity.Property(l => l.LicenseType).HasColumnName("license_type").IsRequired().HasMaxLength(20);
            entity.Property(l => l.Edition).HasColumnName("edition").IsRequired().HasMaxLength(30);
            entity.Property(l => l.Market).HasColumnName("market").IsRequired().HasMaxLength(10);
            entity.Property(l => l.IssuedAtUtc).HasColumnName("issued_at_utc").IsRequired().HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            entity.Property(l => l.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(l => l.IsRevoked).HasColumnName("is_revoked").HasDefaultValue(false);
            entity.Property(l => l.RevokedAtUtc).HasColumnName("revoked_at_utc");

            // Índices
            entity.HasIndex(l => l.MachineFingerprintHash).HasDatabaseName("ix_issued_licenses_hwid");
            entity.HasIndex(l => l.LicenseId).IsUnique().HasDatabaseName("ix_issued_licenses_license_id");
            entity.HasIndex(l => l.IssuedAtUtc).HasDatabaseName("ix_issued_licenses_issued_at");
        });

        // Configuración de LicenseEvents (Auditoría de licencias)
        modelBuilder.Entity<LicenseEvent>(entity =>
        {
            entity.ToTable("license_events");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.LicenseId).HasColumnName("license_id").IsRequired().HasMaxLength(50);
            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(30);
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired().HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

            // Índices
            entity.HasIndex(e => e.LicenseId).HasDatabaseName("ix_license_events_license_id");
            entity.HasIndex(e => e.EventType).HasDatabaseName("ix_license_events_type");
            entity.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("ix_license_events_created_at");
        });

        // Configuración de HardwareBindings
        modelBuilder.Entity<HardwareBinding>(entity =>
        {
            entity.ToTable("hardware_bindings");
            entity.HasKey(h => h.Id);

            entity.Property(h => h.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(h => h.MachineFingerprintHash).HasColumnName("machine_fingerprint_hash").IsRequired().HasMaxLength(64);
            entity.Property(h => h.ComputerName).HasColumnName("computer_name").HasMaxLength(100);
            entity.Property(h => h.CpuId).HasColumnName("cpu_id").HasMaxLength(100);
            entity.Property(h => h.MotherboardId).HasColumnName("motherboard_id").HasMaxLength(100);
            entity.Property(h => h.FirstSeenUtc).HasColumnName("first_seen_utc").IsRequired().HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            entity.Property(h => h.LastSeenUtc).HasColumnName("last_seen_utc");

            // Índices
            entity.HasIndex(h => h.MachineFingerprintHash).IsUnique().HasDatabaseName("ix_hardware_bindings_hwid");
        });
    }
}

/// <summary>
/// Entidad que representa una licencia emitida. Append-only.
/// </summary>
public class IssuedLicense
{
    public int Id { get; set; }
    public string LicenseId { get; set; } = string.Empty;
    public string MachineFingerprintHash { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

/// <summary>
/// Entidad que registra eventos de licencias.
/// </summary>
public class LicenseEvent
{
    public int Id { get; set; }
    public string LicenseId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // "Issued", "Validated", "Renewed", "Revoked", "Expired"
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Entidad que mapea HWID a información de hardware.
/// </summary>
public class HardwareBinding
{
    public int Id { get; set; }
    public string MachineFingerprintHash { get; set; } = string.Empty;
    public string? ComputerName { get; set; }
    public string? CpuId { get; set; }
    public string? MotherboardId { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
}
