using Microsoft.EntityFrameworkCore;
using OS.Domain.Entities;
using OS.Domain.Interfaces;

namespace OS.Infrastructure.Data;

/// <summary>
/// DbContext unificado para el dominio clínico-administrativo.
/// Almacena: Pacientes, expedientes, citas, usuarios, RBAC, comercio, auditoría operativa y cumplimiento.
/// Base de datos: clinicos_db (PostgreSQL portable embebido).
/// </summary>
public sealed class ClinicDbContext : DbContext, IApplicationDbContext
{
    private readonly IRuntimeAccessPolicy? _runtimeAccessPolicy;

    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<MedicalRecord> MedicalRecords { get; set; } = null!;
    public DbSet<ClinicalAmendment> ClinicalAmendments { get; set; } = null!;
    public DbSet<Appointment> Appointments { get; set; } = null!;
    public DbSet<PasswordHistory> PasswordHistories { get; set; } = null!;
    public DbSet<LoginAttempt> LoginAttempts { get; set; } = null!;
    public DbSet<SecurityQuestion> SecurityQuestions { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<AccessLog> AccessLogs { get; set; } = null!;
    public DbSet<UserSession> UserSessions { get; set; } = null!;
    public DbSet<AdminRegistration> AdminRegistrations { get; set; } = null!;
    public DbSet<LicenseDistribution> LicenseDistributions { get; set; } = null!;
    public DbSet<LicenseRequest> LicenseRequests { get; set; } = null!;
    public DbSet<Specialty> Specialties { get; set; } = null!;
    public DbSet<DoctorTitle> DoctorTitles { get; set; } = null!;
    public DbSet<BillingConcept> BillingConcepts { get; set; } = null!;
    public DbSet<PaymentRecord> PaymentRecords { get; set; } = null!;
    public DbSet<CashRegisterSession> CashRegisterSessions { get; set; } = null!;
    public DbSet<WarehouseProduct> WarehouseProducts { get; set; } = null!;
    public DbSet<ProductBatch> ProductBatches { get; set; } = null!;
    public DbSet<StockMovement> StockMovements { get; set; } = null!;
    public DbSet<PrescriptionDispensing> PrescriptionDispensings { get; set; } = null!;
    public DbSet<PrintJob> PrintJobs { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Sale> Sales { get; set; } = null!;
    public DbSet<SaleItem> SaleItems { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<ConsentRecord> ConsentRecords { get; set; } = null!;
    public DbSet<ArcoRequest> ArcoRequests { get; set; } = null!;
    public DbSet<PatientConsent> PatientConsents { get; set; } = null!;
    public DbSet<PrivacyNoticeAcceptance> PrivacyNoticeAcceptances { get; set; } = null!;

#pragma warning disable IL2026, IL3050 // EF Core DbContext construction is isolated to the offline PostgreSQL persistence boundary.
    public ClinicDbContext(DbContextOptions<ClinicDbContext> options, IRuntimeAccessPolicy? runtimeAccessPolicy = null)
        : base(options)
    {
        _runtimeAccessPolicy = runtimeAccessPolicy;
    }
#pragma warning restore IL2026, IL3050

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de Patients
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("patients");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.FirstName).HasColumnName("first_name").IsRequired().HasMaxLength(100);
            entity.Property(p => p.LastName).HasColumnName("last_name").IsRequired().HasMaxLength(100);
            entity.Property(p => p.Cedula).HasColumnName("cedula").HasMaxLength(20);
            entity.Property(p => p.DateOfBirth).HasColumnName("date_of_birth");
            entity.Property(p => p.Gender).HasColumnName("gender");
            entity.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(p => p.UpdatedAt).HasColumnName("updated_at");

            // Índices
            entity.HasIndex(p => p.Cedula).IsUnique().HasDatabaseName("ix_patients_cedula");
            entity.HasIndex(p => p.CreatedAt).HasDatabaseName("ix_patients_created_at");

            // Restricción única (Cedula puede ser NULL pero si existe debe ser único)
            entity.HasIndex(p => p.Cedula)
                .HasFilter("cedula IS NOT NULL")
                .IsUnique()
                .HasDatabaseName("ix_patients_cedula_unique_where_not_null");
        });

        // Configuración de MedicalRecords
        modelBuilder.Entity<MedicalRecord>(entity =>
        {
            entity.ToTable("medical_records");
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Id).HasColumnName("id");
            entity.Property(m => m.PatientId).HasColumnName("patient_id").IsRequired();
            entity.Property(m => m.DoctorId).HasColumnName("doctor_id").IsRequired();
            entity.Property(m => m.Date).HasColumnName("record_date").IsRequired();
            entity.Property(m => m.DiagnosisCode).HasColumnName("diagnosis_code").HasMaxLength(10);
            entity.Property(m => m.Diagnosis).HasColumnName("diagnosis").IsRequired().HasMaxLength(500);
            entity.Property(m => m.Notes).HasColumnName("notes");
            entity.Property(m => m.Treatment).HasColumnName("treatment");
            entity.Property(m => m.Prescription).HasColumnName("prescription");
            entity.Property(m => m.DoctorName).HasColumnName("doctor_name").HasMaxLength(200);
            entity.Property(m => m.IsLocked).HasColumnName("is_locked").IsRequired().HasDefaultValue(false);
            entity.Property(m => m.LockedAtUtc).HasColumnName("locked_at_utc");
            entity.Property(m => m.LockedByUserId).HasColumnName("locked_by_user_id").HasMaxLength(100);
            entity.Property(m => m.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(m => m.UpdatedAt).HasColumnName("updated_at");

            // Relación con Patient
            entity.HasOne<Patient>()
                .WithMany()
                .HasForeignKey(m => m.PatientId)
                .HasConstraintName("fk_medical_records_patient")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(m => m.PatientId).HasDatabaseName("ix_medical_records_patient_id");
            entity.HasIndex(m => new { m.PatientId, m.CreatedAt }).HasDatabaseName("ix_medical_records_patient_created");
            entity.HasIndex(m => m.DoctorId).HasDatabaseName("ix_medical_records_doctor_id");

            // RLS por doctor: política en capa de aplicación (RBAC); ver migraciones futuras para RLS nativo PG.
        });

        modelBuilder.Entity<ClinicalAmendment>(entity =>
        {
            entity.ToTable("clinical_amendments");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.MedicalRecordId).HasColumnName("medical_record_id").IsRequired();
            entity.Property(a => a.AuthorUserId).HasColumnName("author_user_id").IsRequired();
            entity.Property(a => a.AmendedAtUtc).HasColumnName("amended_at_utc").IsRequired().HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            entity.Property(a => a.Reason).HasColumnName("reason").IsRequired().HasMaxLength(500);
            entity.Property(a => a.Content).HasColumnName("content").IsRequired();
            entity.Property(a => a.EvidenceHash).HasColumnName("evidence_hash").IsRequired().HasMaxLength(128);
            entity.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            entity.Property(a => a.UpdatedAt).HasColumnName("updated_at");
            entity.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            entity.Property(a => a.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(a => a.DeletedAt).HasColumnName("deleted_at");

            entity.HasOne(a => a.MedicalRecord)
                .WithMany(m => m.Amendments)
                .HasForeignKey(a => a.MedicalRecordId)
                .HasConstraintName("fk_clinical_amendments_medical_record")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(a => a.MedicalRecordId).HasDatabaseName("ix_clinical_amendments_medical_record");
            entity.HasIndex(a => a.AmendedAtUtc).HasDatabaseName("ix_clinical_amendments_amended_at");
        });

        // Configuración de Appointments
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.ToTable("appointments");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.PatientId).HasColumnName("patient_id").IsRequired();
            entity.Property(a => a.DoctorId).HasColumnName("doctor_id").IsRequired();
            entity.Property(a => a.AppointmentDate).HasColumnName("appointment_date").IsRequired();
            entity.Property(a => a.DurationMinutes).HasColumnName("duration_minutes").HasDefaultValue(30);
            entity.Property(a => a.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(a => a.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(a => a.Notes).HasColumnName("notes");
            entity.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(a => a.UpdatedAt).HasColumnName("updated_at");

            // Relación con Patient
            entity.HasOne<Patient>()
                .WithMany()
                .HasForeignKey(a => a.PatientId)
                .HasConstraintName("fk_appointments_patient")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(a => a.PatientId).HasDatabaseName("ix_appointments_patient_id");
            entity.HasIndex(a => a.AppointmentDate).HasDatabaseName("ix_appointments_date");
            entity.HasIndex(a => new { a.DoctorId, a.AppointmentDate })
                .HasDatabaseName("ix_appointments_doctor_date");
        });

        // Configuración de PasswordHistory
        modelBuilder.Entity<PasswordHistory>(entity =>
        {
            entity.ToTable("password_history");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(p => p.PasswordHash).HasColumnName("password_hash").IsRequired().HasMaxLength(256);
            entity.Property(p => p.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("NOW()");

            // Relación con User
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .HasConstraintName("fk_password_history_user")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(p => p.UserId).HasDatabaseName("ix_password_history_user_id");
            entity.HasIndex(p => new { p.UserId, p.ChangedAt })
                .HasDatabaseName("ix_password_history_user_changed")
                .IsDescending();
        });

        // Configuración de LoginAttempt
        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.ToTable("login_attempts");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.Id).HasColumnName("id");
            entity.Property(l => l.Username).HasColumnName("username").IsRequired().HasMaxLength(100);
            entity.Property(l => l.MachineFingerprintHash).HasColumnName("machine_fingerprint_hash").HasMaxLength(64);
            entity.Property(l => l.AttemptAt).HasColumnName("attempt_at").HasDefaultValueSql("NOW()");
            entity.Property(l => l.IsSuccessful).HasColumnName("is_successful").IsRequired();
            entity.Property(l => l.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(l => l.UserAgent).HasColumnName("user_agent").HasMaxLength(1000);
            entity.Property(l => l.FailureReason).HasColumnName("failure_reason");

            // Índices
            entity.HasIndex(l => l.Username).HasDatabaseName("ix_login_attempts_username");
            entity.HasIndex(l => new { l.Username, l.AttemptAt })
                .HasDatabaseName("ix_login_attempts_username_attempt")
                .IsDescending();
            entity.HasIndex(l => l.MachineFingerprintHash).HasDatabaseName("ix_login_attempts_fingerprint");
        });

        // Configuración de SecurityQuestion
        modelBuilder.Entity<SecurityQuestion>(entity =>
        {
            entity.ToTable("security_questions");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id).HasColumnName("id");
            entity.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(s => s.QuestionIndex).HasColumnName("question_index").IsRequired();
            entity.Property(s => s.QuestionText).HasColumnName("question_text").IsRequired().HasMaxLength(500);
            entity.Property(s => s.AnswerHash).HasColumnName("answer_hash").IsRequired().HasMaxLength(256);
            entity.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            // Relación con User
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .HasConstraintName("fk_security_questions_user")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(s => s.UserId).HasDatabaseName("ix_security_questions_user_id");
            entity.HasIndex(s => new { s.UserId, s.QuestionIndex })
                .HasDatabaseName("ix_security_questions_user_question")
                .IsUnique();

            // Restricción CHECK para question_index entre 1 y 3
            entity.HasData(
                // La restricción CHECK se agregará en la migración
            );
        });

        // Configuración de Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.Code).HasColumnName("code").IsRequired().HasMaxLength(50);
            entity.Property(r => r.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            entity.Property(r => r.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            // Índices
            entity.HasIndex(r => r.Code).HasDatabaseName("ix_roles_code").IsUnique();
        });

        // Configuración de Permission
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.Code).HasColumnName("code").IsRequired().HasMaxLength(100);
            entity.Property(p => p.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            entity.Property(p => p.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(p => p.Module).HasColumnName("module").IsRequired().HasMaxLength(50);
            entity.Property(p => p.Level).HasColumnName("level").IsRequired().HasMaxLength(20);
            entity.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            // Índices
            entity.HasIndex(p => p.Code).HasDatabaseName("ix_permissions_code").IsUnique();
            entity.HasIndex(p => new { p.Module, p.Level }).HasDatabaseName("ix_permissions_module_level");
        });

        // Configuración de RolePermission
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(rp => rp.Id);

            entity.Property(rp => rp.Id).HasColumnName("id");
            entity.Property(rp => rp.RoleId).HasColumnName("role_id").IsRequired();
            entity.Property(rp => rp.PermissionId).HasColumnName("permission_id").IsRequired();
            entity.Property(rp => rp.AssignedAt).HasColumnName("assigned_at").HasDefaultValueSql("NOW()");
            entity.Property(rp => rp.AssignedBy).HasColumnName("assigned_by");

            // Relaciones
            entity.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .HasConstraintName("fk_role_permissions_role")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .HasConstraintName("fk_role_permissions_permission")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(rp => new { rp.RoleId, rp.PermissionId })
                .HasDatabaseName("ix_role_permissions_role_permission")
                .IsUnique();
        });

        // Configuración de UserRole
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(ur => ur.Id);

            entity.Property(ur => ur.Id).HasColumnName("id");
            entity.Property(ur => ur.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(ur => ur.RoleId).HasColumnName("role_id").IsRequired();
            entity.Property(ur => ur.AssignedAt).HasColumnName("assigned_at").HasDefaultValueSql("NOW()");
            entity.Property(ur => ur.AssignedBy).HasColumnName("assigned_by");
            entity.Property(ur => ur.RevokedAt).HasColumnName("revoked_at");
            entity.Property(ur => ur.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(500);

            // Relaciones
            entity.HasOne(ur => ur.User)
                .WithMany()
                .HasForeignKey(ur => ur.UserId)
                .HasConstraintName("fk_user_roles_user")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .HasConstraintName("fk_user_roles_role")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(ur => ur.UserId).HasDatabaseName("ix_user_roles_user_id");
            entity.HasIndex(ur => new { ur.UserId, ur.RoleId })
                .HasDatabaseName("ix_user_roles_user_role")
                .IsUnique();
            entity.HasIndex(ur => ur.RevokedAt).HasDatabaseName("ix_user_roles_revoked_at");
        });

        // Configuración de AccessLog
        modelBuilder.Entity<AccessLog>(entity =>
        {
            entity.ToTable("access_logs");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(a => a.Resource).HasColumnName("resource").IsRequired().HasMaxLength(200);
            entity.Property(a => a.Action).HasColumnName("action").IsRequired().HasMaxLength(50);
            entity.Property(a => a.Granted).HasColumnName("granted").IsRequired();
            entity.Property(a => a.DenialReason).HasColumnName("denial_reason").HasMaxLength(500);
            entity.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(a => a.UserAgent).HasColumnName("user_agent").HasMaxLength(1000);
            entity.Property(a => a.AttemptAt).HasColumnName("attempt_at").HasDefaultValueSql("NOW()");

            // Relación con User
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .HasConstraintName("fk_access_logs_user")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(a => a.UserId).HasDatabaseName("ix_access_logs_user_id");
            entity.HasIndex(a => a.Granted).HasDatabaseName("ix_access_logs_granted");
            entity.HasIndex(a => new { a.UserId, a.AttemptAt })
                .HasDatabaseName("ix_access_logs_user_attempt")
                .IsDescending();
            entity.HasIndex(a => new { a.Granted, a.AttemptAt })
                .HasDatabaseName("ix_access_logs_granted_attempt")
                .IsDescending();
        });

        // Configuración de UserSession
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Id).HasColumnName("id");
            entity.Property(u => u.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(u => u.MachineFingerprintHash).HasColumnName("machine_fingerprint_hash").IsRequired().HasMaxLength(64);
            entity.Property(u => u.SessionToken).HasColumnName("session_token").IsRequired().HasMaxLength(512);
            entity.Property(u => u.LoginAtUtc).HasColumnName("login_at_utc").IsRequired().HasDefaultValueSql("NOW()");
            entity.Property(u => u.LastActivityAtUtc).HasColumnName("last_activity_at_utc").IsRequired().HasDefaultValueSql("NOW()");
            entity.Property(u => u.LogoutAtUtc).HasColumnName("logout_at_utc");
            entity.Property(u => u.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValueSql("true");
            entity.Property(u => u.LogoutReason).HasColumnName("logout_reason").HasMaxLength(100);
            entity.Property(u => u.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(u => u.UserAgent).HasColumnName("user_agent").HasMaxLength(1000);

            // Relación con User
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(u => u.UserId)
                .HasConstraintName("fk_user_sessions_user")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(u => u.MachineFingerprintHash).HasDatabaseName("ix_user_sessions_fingerprint");
            entity.HasIndex(u => u.IsActive).HasDatabaseName("ix_user_sessions_is_active");
            entity.HasIndex(u => u.SessionToken).HasDatabaseName("ix_user_sessions_token").IsUnique();
            entity.HasIndex(u => new { u.UserId, u.IsActive }).HasDatabaseName("ix_user_sessions_user_active");
            entity.HasIndex(u => new { u.UserId, u.MachineFingerprintHash, u.IsActive })
                .HasDatabaseName("ix_user_sessions_user_fingerprint_active");

            // Restricción CHECK para logout_at_utc
            // La restricción CHECK se agregará en la migración
        });

        // Configuración de AdminRegistration
        modelBuilder.Entity<AdminRegistration>(entity =>
        {
            entity.ToTable("admin_registrations");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(a => a.MembershipLevel).HasColumnName("membership_level").IsRequired();
            entity.Property(a => a.MaxHwidLimit).HasColumnName("max_hwid_limit").IsRequired();
            entity.Property(a => a.OrganizationName).HasColumnName("organization_name").IsRequired().HasMaxLength(256);
            entity.Property(a => a.ContactEmail).HasColumnName("contact_email").IsRequired().HasMaxLength(256);
            entity.Property(a => a.PlanStartDate).HasColumnName("plan_start_date").IsRequired().HasDefaultValueSql("NOW()");
            entity.Property(a => a.PlanEndDate).HasColumnName("plan_end_date");
            entity.Property(a => a.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValueSql("true");
            entity.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(a => a.UpdatedAt).HasColumnName("updated_at");

            // Relación con User
            entity.HasOne<User>()
                .WithOne()
                .HasForeignKey<AdminRegistration>(a => a.UserId)
                .HasConstraintName("fk_admin_registrations_user")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(a => a.UserId).HasDatabaseName("ix_admin_registrations_user_id").IsUnique();
            entity.HasIndex(a => a.MembershipLevel).HasDatabaseName("ix_admin_registrations_membership_level");
            entity.HasIndex(a => a.IsActive).HasDatabaseName("ix_admin_registrations_is_active");
        });

        // Configuración de LicenseDistribution
        modelBuilder.Entity<LicenseDistribution>(entity =>
        {
            entity.ToTable("license_distributions");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.Id).HasColumnName("id");
            entity.Property(l => l.AdminRegistrationId).HasColumnName("admin_registration_id").IsRequired();
            entity.Property(l => l.HwidHash).HasColumnName("hwid_hash").IsRequired().HasMaxLength(64);
            entity.Property(l => l.DistributedAt).HasColumnName("distributed_at").IsRequired().HasDefaultValueSql("NOW()");
            entity.Property(l => l.DistributionMethod).HasColumnName("distribution_method").IsRequired().HasMaxLength(100);
            entity.Property(l => l.Notes).HasColumnName("notes");
            entity.Property(l => l.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValueSql("true");
            entity.Property(l => l.ActivatedAt).HasColumnName("activated_at");
            entity.Property(l => l.RequestId).HasColumnName("request_id");
            entity.Property(l => l.RevokedAt).HasColumnName("revoked_at");
            entity.Property(l => l.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(500);

            // Relación con LicenseRequest
            entity.HasOne<LicenseRequest>()
                .WithMany()
                .HasForeignKey(l => l.RequestId)
                .HasConstraintName("fk_license_distributions_request")
                .OnDelete(DeleteBehavior.Restrict);

            // Relación con AdminRegistration
            entity.HasOne<AdminRegistration>()
                .WithMany(a => a.LicenseDistributions)
                .HasForeignKey(l => l.AdminRegistrationId)
                .HasConstraintName("fk_license_distributions_admin")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(l => l.AdminRegistrationId).HasDatabaseName("ix_license_distributions_admin_id");
            entity.HasIndex(l => l.HwidHash).HasDatabaseName("ix_license_distributions_hwid");
            entity.HasIndex(l => l.IsActive).HasDatabaseName("ix_license_distributions_is_active");
            entity.HasIndex(l => new { l.AdminRegistrationId, l.HwidHash })
                .HasDatabaseName("ix_license_distributions_admin_hwid")
                .IsUnique();
        });

        // Configuración de LicenseRequest
        modelBuilder.Entity<LicenseRequest>(entity =>
        {
            entity.ToTable("license_requests");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.Id).HasColumnName("id");
            entity.Property(l => l.AdminRegistrationId).HasColumnName("admin_registration_id").IsRequired();
            entity.Property(l => l.HwidHash).HasColumnName("hwid_hash").IsRequired().HasMaxLength(64);
            entity.Property(l => l.ClinicName).HasColumnName("clinic_name").HasMaxLength(256);
            entity.Property(l => l.ContactPhone).HasColumnName("contact_phone").HasMaxLength(50);
            entity.Property(l => l.RequestedLicenses).HasColumnName("requested_licenses").IsRequired().HasDefaultValue(1);
            entity.Property(l => l.RequesterName).HasColumnName("requester_name").IsRequired().HasMaxLength(256);
            entity.Property(l => l.RequesterEmail).HasColumnName("requester_email").IsRequired().HasMaxLength(256);
            entity.Property(l => l.IsSent).HasColumnName("is_sent").IsRequired().HasDefaultValue(false);
            entity.Property(l => l.SentAt).HasColumnName("sent_at");
            entity.Property(l => l.Status).HasColumnName("status").IsRequired();
            entity.Property(l => l.RequestedAt).HasColumnName("requested_at").IsRequired().HasDefaultValueSql("NOW()");
            entity.Property(l => l.ProcessedAt).HasColumnName("processed_at");
            entity.Property(l => l.ProcessedBy).HasColumnName("processed_by");
            entity.Property(l => l.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(l => l.ExpiresAt).HasColumnName("expires_at");

            // Relación con AdminRegistration
            entity.HasOne<AdminRegistration>()
                .WithMany(a => a.LicenseRequests)
                .HasForeignKey(l => l.AdminRegistrationId)
                .HasConstraintName("fk_license_requests_admin")
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(l => l.AdminRegistrationId).HasDatabaseName("ix_license_requests_admin_id");
            entity.HasIndex(l => l.HwidHash).HasDatabaseName("ix_license_requests_hwid");
            entity.HasIndex(l => l.Status).HasDatabaseName("ix_license_requests_status");
            entity.HasIndex(l => new { l.AdminRegistrationId, l.HwidHash })
                .HasDatabaseName("ix_license_requests_admin_hwid");
            entity.HasIndex(l => new { l.Status, l.RequestedAt })
                .HasDatabaseName("ix_license_requests_status_requested")
                .IsDescending();
        });

        // Configuración de Specialty
        modelBuilder.Entity<Specialty>(entity =>
        {
            entity.ToTable("specialties");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id).HasColumnName("id");
            entity.Property(s => s.Code).HasColumnName("code").IsRequired().HasMaxLength(20);
            entity.Property(s => s.Name).HasColumnName("name").IsRequired().HasMaxLength(256);
            entity.Property(s => s.Description).HasColumnName("description");
            entity.Property(s => s.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValueSql("true");
            entity.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(s => s.CreatedBy).HasColumnName("created_by").IsRequired();
            entity.Property(s => s.UpdatedAt).HasColumnName("updated_at");
            entity.Property(s => s.UpdatedBy).HasColumnName("updated_by");

            // Relación con User (Creator)
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.CreatedBy)
                .HasConstraintName("fk_specialties_creator")
                .OnDelete(DeleteBehavior.Restrict);

            // Relación con User (Updater)
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.UpdatedBy)
                .HasConstraintName("fk_specialties_updater")
                .OnDelete(DeleteBehavior.Restrict);

            // Índices
            entity.HasIndex(s => s.Code).HasDatabaseName("ix_specialties_code").IsUnique();
            entity.HasIndex(s => s.IsActive).HasDatabaseName("ix_specialties_is_active");
        });

        // Configuración de DoctorTitle
        modelBuilder.Entity<DoctorTitle>(entity =>
        {
            entity.ToTable("doctor_titles");
            entity.HasKey(d => d.Id);

            entity.Property(d => d.Id).HasColumnName("id");
            entity.Property(d => d.SpecialtyId).HasColumnName("specialty_id").IsRequired();
            entity.Property(d => d.Title).HasColumnName("title").IsRequired().HasMaxLength(256);
            entity.Property(d => d.Abbreviation).HasColumnName("abbreviation").HasMaxLength(20);
            entity.Property(d => d.IsPrimary).HasColumnName("is_primary").IsRequired().HasDefaultValueSql("false");
            entity.Property(d => d.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(d => d.CreatedBy).HasColumnName("created_by").IsRequired();

            // Relación con Specialty
            entity.HasOne<Specialty>()
                .WithMany(s => s.DoctorTitles)
                .HasForeignKey(d => d.SpecialtyId)
                .HasConstraintName("fk_doctor_titles_specialty")
                .OnDelete(DeleteBehavior.Restrict);

            // Relación con User (Creator)
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("fk_doctor_titles_creator")
                .OnDelete(DeleteBehavior.Restrict);

            // Índices
            entity.HasIndex(d => d.SpecialtyId).HasDatabaseName("ix_doctor_titles_specialty_id");
            entity.HasIndex(d => new { d.SpecialtyId, d.Title })
                .HasDatabaseName("ix_doctor_titles_specialty_title")
                .IsUnique();
        });

        // Configuración de BillingConcept
        modelBuilder.Entity<BillingConcept>(entity =>
        {
            entity.ToTable("billing_concepts");
            entity.HasKey(b => b.Id);

            entity.Property(b => b.Id).HasColumnName("id");
            entity.Property(b => b.Name).HasColumnName("name").IsRequired().HasMaxLength(256);
            entity.Property(b => b.BasePrice).HasColumnName("base_price").IsRequired().HasPrecision(12, 2);
            entity.Property(b => b.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValueSql("true");
            entity.Property(b => b.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            // Índices
            entity.HasIndex(b => b.Name).HasDatabaseName("ix_billing_concepts_name").IsUnique();
        });

        // Configuración de PaymentRecord
        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.ToTable("payment_records");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.PatientId).HasColumnName("patient_id");
            entity.Property(p => p.AppointmentId).HasColumnName("appointment_id");
            entity.Property(p => p.ConceptId).HasColumnName("concept_id").IsRequired();
            entity.Property(p => p.Amount).HasColumnName("amount").IsRequired().HasPrecision(12, 2);
            entity.Property(p => p.DiscountAmount).HasColumnName("discount_amount").HasPrecision(12, 2).HasDefaultValueSql("0");
            entity.Property(p => p.DiscountReason).HasColumnName("discount_reason");
            entity.Property(p => p.PaymentMethod).HasColumnName("payment_method").IsRequired();
            entity.Property(p => p.Folio).HasColumnName("folio").IsRequired().HasMaxLength(50);
            entity.Property(p => p.TransactionHash).HasColumnName("transaction_hash").IsRequired().HasMaxLength(64);
            entity.Property(p => p.ReceiptPdfPath).HasColumnName("receipt_pdf_path").HasMaxLength(512);
            entity.Property(p => p.CashierId).HasColumnName("cashier_id").IsRequired();
            entity.Property(p => p.PaymentDate).HasColumnName("payment_date").IsRequired().HasDefaultValueSql("NOW()");
            entity.Property(p => p.Notes).HasColumnName("notes");
            entity.Property(p => p.IsVoided).HasColumnName("is_voided").IsRequired().HasDefaultValueSql("false");
            entity.Property(p => p.VoidReason).HasColumnName("void_reason");
            entity.Property(p => p.VoidBy).HasColumnName("void_by");
            entity.Property(p => p.VoidAt).HasColumnName("void_at");

            // Relación con BillingConcept
            entity.HasOne<BillingConcept>()
                .WithMany()
                .HasForeignKey(p => p.ConceptId)
                .HasConstraintName("fk_payment_records_concept")
                .OnDelete(DeleteBehavior.Restrict);

            // Índices
            entity.HasIndex(p => p.PatientId).HasDatabaseName("ix_payment_records_patient");
            entity.HasIndex(p => p.Folio).HasDatabaseName("ix_payment_records_folio").IsUnique();
        });

        // Configuración de CashRegisterSession
        modelBuilder.Entity<CashRegisterSession>(entity =>
        {
            entity.ToTable("cash_register_sessions");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.CashierId).HasColumnName("cashier_id").IsRequired();
            entity.Property(c => c.OpenedAt).HasColumnName("opened_at").IsRequired().HasDefaultValueSql("NOW()");
            entity.Property(c => c.ClosedAt).HasColumnName("closed_at");
            entity.Property(c => c.OpeningAmount).HasColumnName("opening_amount").IsRequired().HasPrecision(12, 2);
            entity.Property(c => c.DeclaredClosingAmount).HasColumnName("declared_closing_amount").HasPrecision(12, 2);
            entity.Property(c => c.CalculatedClosingAmount).HasColumnName("calculated_closing_amount").HasPrecision(12, 2);
            entity.Property(c => c.Status).HasColumnName("status").IsRequired();
            entity.Property(c => c.PreviousSessionId).HasColumnName("previous_session_id");

            // Relación con sesión anterior
            entity.HasOne<CashRegisterSession>()
                .WithMany()
                .HasForeignKey(c => c.PreviousSessionId)
                .HasConstraintName("fk_cash_sessions_previous")
                .OnDelete(DeleteBehavior.Restrict);

            // Índices
            entity.HasIndex(c => new { c.CashierId, c.Status }).HasDatabaseName("ix_cash_sessions_cashier_status");
        });

        // Configuración de WarehouseProduct
        modelBuilder.Entity<WarehouseProduct>(entity =>
        {
            entity.ToTable("warehouse_products");
            entity.HasKey(w => w.Id);

            entity.Property(w => w.Id).HasColumnName("id");
            entity.Property(w => w.SkuCode).HasColumnName("sku_code").IsRequired().HasMaxLength(50);
            entity.Property(w => w.Name).HasColumnName("name").IsRequired().HasMaxLength(256);
            entity.Property(w => w.UnitOfMeasure).HasColumnName("unit_of_measure").IsRequired().HasMaxLength(30);
            entity.Property(w => w.MinStock).HasColumnName("min_stock").IsRequired().HasDefaultValueSql("0");
            entity.Property(w => w.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValueSql("true");
            entity.Property(w => w.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            // Índices
            entity.HasIndex(w => w.SkuCode).HasDatabaseName("ix_warehouse_sku").IsUnique();
        });

        // Configuración de ProductBatch
        modelBuilder.Entity<ProductBatch>(entity =>
        {
            entity.ToTable("product_batches");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.ProductId).HasColumnName("product_id").IsRequired();
            entity.Property(p => p.BatchCode).HasColumnName("batch_code").IsRequired().HasMaxLength(100);
            entity.Property(p => p.ExpirationDate).HasColumnName("expiration_date").IsRequired();
            entity.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            // Relación con WarehouseProduct
            entity.HasOne<WarehouseProduct>()
                .WithMany(w => w.ProductBatches)
                .HasForeignKey(p => p.ProductId)
                .HasConstraintName("fk_product_batches_product")
                .OnDelete(DeleteBehavior.Restrict);

            // Índices
            entity.HasIndex(p => new { p.ProductId, p.BatchCode })
                .HasDatabaseName("ix_product_batches_product_batch")
                .IsUnique();
            entity.HasIndex(p => new { p.ProductId, p.ExpirationDate })
                .HasDatabaseName("ix_stock_batches");
        });

        // Configuración de StockMovement
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id).HasColumnName("id");
            entity.Property(s => s.BatchId).HasColumnName("batch_id").IsRequired();
            entity.Property(s => s.MovementType).HasColumnName("movement_type").IsRequired();
            entity.Property(s => s.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(s => s.MovementHash).HasColumnName("movement_hash").IsRequired().HasMaxLength(64);
            entity.Property(s => s.JustificationText).HasColumnName("justification_text");
            entity.Property(s => s.ReferenceDocument).HasColumnName("reference_document").HasMaxLength(100);
            entity.Property(s => s.OperatorId).HasColumnName("operator_id").IsRequired();
            entity.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            // Relación con ProductBatch
            entity.HasOne<ProductBatch>()
                .WithMany(b => b.StockMovements)
                .HasForeignKey(s => s.BatchId)
                .HasConstraintName("fk_stock_movements_batch")
                .OnDelete(DeleteBehavior.Restrict);

            // Índices
            entity.HasIndex(s => new { s.BatchId, s.CreatedAt })
                .HasDatabaseName("ix_stock_chain");
        });

        // Configuración de PrescriptionDispensing
        modelBuilder.Entity<PrescriptionDispensing>(entity =>
        {
            entity.ToTable("prescription_dispensing");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.MedicalRecordId).HasColumnName("medical_record_id").IsRequired();
            entity.Property(p => p.BatchId).HasColumnName("batch_id").IsRequired();
            entity.Property(p => p.QuantityDispensed).HasColumnName("quantity_dispensed").IsRequired();
            entity.Property(p => p.QuantityPrescribed).HasColumnName("quantity_prescribed").IsRequired();
            entity.Property(p => p.Status).HasColumnName("status").IsRequired();
            entity.Property(p => p.NoteToDoctor).HasColumnName("note_to_doctor");
            entity.Property(p => p.DispensedAt).HasColumnName("dispensed_at").IsRequired().HasDefaultValueSql("NOW()");
            entity.Property(p => p.PharmacistId).HasColumnName("pharmacist_id").IsRequired();
            entity.Property(p => p.BatchExpirationDate).HasColumnName("batch_expiration_date").IsRequired();

            // Relación con MedicalRecord
            entity.HasOne<MedicalRecord>()
                .WithMany()
                .HasForeignKey(p => p.MedicalRecordId)
                .HasConstraintName("fk_prescription_dispensing_medical_record")
                .OnDelete(DeleteBehavior.Restrict);

            // Relación con ProductBatch
            entity.HasOne<ProductBatch>()
                .WithMany()
                .HasForeignKey(p => p.BatchId)
                .HasConstraintName("fk_prescription_dispensing_batch")
                .OnDelete(DeleteBehavior.Restrict);

            // Índices
            entity.HasIndex(p => p.MedicalRecordId).HasDatabaseName("ix_prescription_dispensing_medical_record");
            entity.HasIndex(p => p.BatchId).HasDatabaseName("ix_prescription_dispensing_batch");
            entity.HasIndex(p => p.Status).HasDatabaseName("ix_prescription_dispensing_status");
        });

        // Configuración de PrintJob
        modelBuilder.Entity<PrintJob>(entity =>
        {
            entity.ToTable("print_jobs");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.DocumentType).HasColumnName("document_type").IsRequired();
            entity.Property(p => p.DocumentId).HasColumnName("document_id").IsRequired().HasMaxLength(100);
            entity.Property(p => p.DocumentHash).HasColumnName("document_hash").IsRequired().HasMaxLength(64);
            entity.Property(p => p.RequestedBy).HasColumnName("requested_by").IsRequired();
            entity.Property(p => p.RequestedAt).HasColumnName("requested_at").IsRequired().HasDefaultValueSql("NOW()");
            entity.Property(p => p.PrintedAt).HasColumnName("printed_at");
            entity.Property(p => p.Copies).HasColumnName("copies").IsRequired().HasDefaultValueSql("1");
            entity.Property(p => p.IsSuccessful).HasColumnName("is_successful").IsRequired().HasDefaultValueSql("false");
            entity.Property(p => p.PrinterName).HasColumnName("printer_name").HasMaxLength(200);
            entity.Property(p => p.SecureWatermarkToken).HasColumnName("secure_watermark_token").IsRequired().HasMaxLength(256);
            entity.Property(p => p.ErrorMessage).HasColumnName("error_message");

            // Relación con User (Requester)
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(p => p.RequestedBy)
                .HasConstraintName("fk_print_jobs_requester")
                .OnDelete(DeleteBehavior.Restrict);

            // Índices
            entity.HasIndex(p => p.DocumentId).HasDatabaseName("ix_print_document");
            entity.HasIndex(p => p.RequestedBy).HasDatabaseName("ix_print_audit_user");
            entity.HasIndex(p => p.SecureWatermarkToken).HasDatabaseName("ix_print_watermark").IsUnique();
        });

        ClinicDbContextCommerceComplianceConfiguration.Apply(modelBuilder);
    }

    /// <summary>
    /// Sobrescribir SaveChanges para agregar metadata automáticamente
    /// </summary>
    public override int SaveChanges()
    {
        EnforceRuntimeReadOnlyPolicy();
        EnforceClinicalImmutability();
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceRuntimeReadOnlyPolicy();
        EnforceClinicalImmutability();
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void EnforceRuntimeReadOnlyPolicy()
    {
        if (_runtimeAccessPolicy?.IsReadOnly != true)
            return;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException($"Modo Solo Lectura activo por licencia: {_runtimeAccessPolicy.Reason}");
        }
    }

    private void EnforceClinicalImmutability()
    {
        foreach (var entry in ChangeTracker.Entries<IClosedClinicalRecord>())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
                continue;

            var originalIsLocked = entry.State == EntityState.Deleted
                ? entry.Entity.IsLocked
                : entry.Property(record => record.IsLocked).OriginalValue;

            if (originalIsLocked)
                throw new InvalidOperationException("Los registros clínicos cerrados son inmutables según NOM-024/HIPAA.");
        }
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries();

        foreach (var entry in entries)
        {
            if (entry.Entity is not Patient entity)
                continue;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
