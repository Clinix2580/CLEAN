using Microsoft.EntityFrameworkCore;

namespace OS.Infrastructure.Data;

/// <summary>
/// DbContext para el dominio de auditoría.
/// Almacena: Logs de auditoría, accesos a PHI, cambios de configuración, eventos de backup.
/// Base de datos: audit_db
/// Acceso: Append-only, inmutable desde la aplicación
/// </summary>
public sealed class AuditDbContext : DbContext
{
    public DbSet<AuditLogEntry> AuditLogs { get; set; } = null!;
    public DbSet<AccessLogEntry> AccessLogs { get; set; } = null!;
    public DbSet<ConfigurationChange> ConfigurationChanges { get; set; } = null!;
    public DbSet<BackupEvent> BackupEvents { get; set; } = null!;

#pragma warning disable IL2026, IL3050 // EF Core DbContext construction is isolated to the append-only audit persistence boundary.
    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }
#pragma warning restore IL2026, IL3050

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de AuditLogs (Append-only)
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(a => a.TableName).HasColumnName("table_name").IsRequired().HasMaxLength(50);
            entity.Property(a => a.Operation).HasColumnName("operation").IsRequired().HasMaxLength(10);
            entity.Property(a => a.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(100);
            entity.Property(a => a.RecordId).HasColumnName("record_id");
            entity.Property(a => a.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
            entity.Property(a => a.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
            entity.Property(a => a.ChangedAtUtc).HasColumnName("changed_at_utc").IsRequired().HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            entity.Property(a => a.IpAddress).HasColumnName("ip_address");
            entity.Property(a => a.VerificationHash).HasColumnName("verification_hash").HasMaxLength(128);
            entity.Property(a => a.PreviousHash).HasColumnName("previous_hash").HasMaxLength(128);

            // Índices
            entity.HasIndex(a => a.TableName).HasDatabaseName("ix_audit_logs_table");
            entity.HasIndex(a => a.UserId).HasDatabaseName("ix_audit_logs_user");
            entity.HasIndex(a => a.ChangedAtUtc).HasDatabaseName("ix_audit_logs_changed_at");
            entity.HasIndex(a => new { a.TableName, a.RecordId }).HasDatabaseName("ix_audit_logs_table_record");
        });

        // Configuración de AccessLogs (Acceso a PHI)
        modelBuilder.Entity<AccessLogEntry>(entity =>
        {
            entity.ToTable("access_logs");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(a => a.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(100);
            entity.Property(a => a.TableName).HasColumnName("table_name").IsRequired().HasMaxLength(50);
            entity.Property(a => a.Operation).HasColumnName("operation").IsRequired().HasMaxLength(10);
            entity.Property(a => a.RecordId).HasColumnName("record_id");
            entity.Property(a => a.AccessedAtUtc).HasColumnName("accessed_at_utc").IsRequired().HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            entity.Property(a => a.IpAddress).HasColumnName("ip_address");
            entity.Property(a => a.Success).HasColumnName("success").HasDefaultValue(true);
            entity.Property(a => a.FailureReason).HasColumnName("failure_reason");
            entity.Property(a => a.VerificationHash).HasColumnName("verification_hash").HasMaxLength(128);
            entity.Property(a => a.PreviousHash).HasColumnName("previous_hash").HasMaxLength(128);

            // Índices
            entity.HasIndex(a => a.UserId).HasDatabaseName("ix_access_logs_user");
            entity.HasIndex(a => new { a.TableName, a.RecordId }).HasDatabaseName("ix_access_logs_table_record");
            entity.HasIndex(a => a.AccessedAtUtc).HasDatabaseName("ix_access_logs_accessed_at");
        });

        // Configuración de ConfigurationChanges
        modelBuilder.Entity<ConfigurationChange>(entity =>
        {
            entity.ToTable("configuration_changes");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(c => c.ConfigKey).HasColumnName("config_key").IsRequired().HasMaxLength(100);
            entity.Property(c => c.OldValue).HasColumnName("old_value");
            entity.Property(c => c.NewValue).HasColumnName("new_value");
            entity.Property(c => c.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired().HasMaxLength(100);
            entity.Property(c => c.ChangedAtUtc).HasColumnName("changed_at_utc").IsRequired().HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            entity.Property(c => c.Reason).HasColumnName("reason");

            // Índices
            entity.HasIndex(c => c.ConfigKey).HasDatabaseName("ix_config_changes_key");
            entity.HasIndex(c => c.ChangedAtUtc).HasDatabaseName("ix_config_changes_changed_at");
        });

        // Configuración de BackupEvents
        modelBuilder.Entity<BackupEvent>(entity =>
        {
            entity.ToTable("backup_events");
            entity.HasKey(b => b.Id);

            entity.Property(b => b.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(b => b.BackupType).HasColumnName("backup_type").IsRequired().HasMaxLength(20);
            entity.Property(b => b.Status).HasColumnName("status").IsRequired().HasMaxLength(20);
            entity.Property(b => b.FilePath).HasColumnName("file_path").HasMaxLength(500);
            entity.Property(b => b.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(b => b.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
            entity.Property(b => b.CompletedAtUtc).HasColumnName("completed_at_utc");
            entity.Property(b => b.ErrorMessage).HasColumnName("error_message");
            entity.Property(b => b.TriggeredByUserId).HasColumnName("triggered_by_user_id").HasMaxLength(100);
            entity.Property(b => b.VerificationHash).HasColumnName("verification_hash");

            // Índices
            entity.HasIndex(b => b.Status).HasDatabaseName("ix_backup_events_status");
            entity.HasIndex(b => b.StartedAtUtc).HasDatabaseName("ix_backup_events_started_at");
        });
    }
}

/// <summary>
/// Entidad que representa un registro de auditoría general. Append-only.
/// </summary>
public class AuditLogEntry
{
    public long Id { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? VerificationHash { get; set; }
    public string? PreviousHash { get; set; }
}

/// <summary>
/// Entidad que registra un acceso a PHI.
/// </summary>
public class AccessLogEntry
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public DateTime AccessedAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public string? VerificationHash { get; set; }
    public string? PreviousHash { get; set; }
}

/// <summary>
/// Entidad que representa un cambio de configuración.
/// </summary>
public class ConfigurationChange
{
    public long Id { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Entidad que registra eventos de backup/restore.
/// </summary>
public class BackupEvent
{
    public int Id { get; set; }
    public string BackupType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TriggeredByUserId { get; set; }
    public string? VerificationHash { get; set; }
}
