using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OS.Domain.Entities;

namespace OS.Infrastructure.Data;

/// <summary>
/// Mapeos EF para entidades de comercio, auditoría operativa (hash chain) y cumplimiento (LFPDPPP/HIPAA).
/// </summary>
internal static class ClinicDbContextCommerceComplianceConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        ConfigureUsers(modelBuilder);
        ConfigureProducts(modelBuilder);
        ConfigureSales(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigureConsentRecords(modelBuilder);
        ConfigureArcoRequests(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Id).HasColumnName("id");
            entity.Property(u => u.Username).HasColumnName("username").IsRequired().HasMaxLength(256);
            entity.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(256);
            entity.Property(u => u.Email).HasColumnName("email").IsRequired().HasMaxLength(256);
            entity.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(u => u.Role).HasColumnName("role").HasMaxLength(50);
            entity.Property(u => u.LastLogin).HasColumnName("last_login");
            entity.Property(u => u.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(u => u.Permissions)
                .HasColumnName("permissions")
                .HasColumnType("jsonb")
#pragma warning disable IL2026, IL3050 // EF value converter serializes List<string>, a closed primitive collection used only for local RBAC permissions.
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    new ValueComparer<List<string>>(
                        (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
                        value => (value ?? new List<string>()).Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                        value => (value ?? new List<string>()).ToList()));
#pragma warning restore IL2026, IL3050
            entity.Property(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(u => u.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            entity.Property(u => u.UpdatedAt).HasColumnName("updated_at");
            entity.Property(u => u.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            entity.Property(u => u.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(u => u.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(u => u.Username).HasDatabaseName("ix_users_username").IsUnique();
            entity.HasIndex(u => u.Email).HasDatabaseName("ix_users_email").IsUnique();
            entity.HasIndex(u => u.IsDeleted).HasDatabaseName("ix_users_is_deleted");

            entity.HasQueryFilter(u => !u.IsDeleted);
        });
    }

    private static void ConfigureProducts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.Code).HasColumnName("code").IsRequired().HasMaxLength(100);
            entity.Property(p => p.Name).HasColumnName("name").IsRequired().HasMaxLength(256);
            entity.Property(p => p.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(p => p.Price).HasColumnName("price").HasPrecision(18, 2);
            entity.Property(p => p.Cost).HasColumnName("cost").HasPrecision(18, 2);
            entity.Property(p => p.StockQuantity).HasColumnName("stock_quantity");
            entity.Property(p => p.MinStockLevel).HasColumnName("min_stock_level");
            entity.Property(p => p.Category).HasColumnName("category").HasMaxLength(100);
            entity.Property(p => p.Barcode).HasColumnName("barcode").HasMaxLength(100);
            entity.Property(p => p.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            entity.Property(p => p.UpdatedAt).HasColumnName("updated_at");
            entity.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            entity.Property(p => p.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(p => p.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(p => p.Code).HasDatabaseName("ix_products_code").IsUnique();
            entity.HasIndex(p => p.Barcode).HasDatabaseName("ix_products_barcode");
            entity.HasIndex(p => p.IsDeleted).HasDatabaseName("ix_products_is_deleted");

            entity.HasQueryFilter(p => !p.IsDeleted);
        });
    }

    private static void ConfigureSales(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.ToTable("sales");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id).HasColumnName("id");
            entity.Property(s => s.Date).HasColumnName("sale_date").IsRequired();
            entity.Property(s => s.Customer).HasColumnName("customer").HasMaxLength(256);
            entity.Property(s => s.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);
            entity.Property(s => s.PaymentMethod).HasColumnName("payment_method").HasMaxLength(50);
            entity.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            entity.Property(s => s.UpdatedAt).HasColumnName("updated_at");
            entity.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            entity.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(s => s.DeletedAt).HasColumnName("deleted_at");

            entity.HasMany(s => s.Items)
                .WithOne()
                .HasForeignKey(si => si.SaleId)
                .HasConstraintName("fk_sale_items_sale")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => s.Date).HasDatabaseName("ix_sales_date");
            entity.HasQueryFilter(s => !s.IsDeleted);
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.ToTable("sale_items");
            entity.HasKey(si => si.Id);

            entity.Property(si => si.Id).HasColumnName("id");
            entity.Property(si => si.SaleId).HasColumnName("sale_id").IsRequired();
            entity.Property(si => si.ProductId).HasColumnName("product_id").IsRequired();
            entity.Property(si => si.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(si => si.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
            entity.Property(si => si.Total).HasColumnName("total").HasPrecision(18, 2);

            entity.HasIndex(si => si.SaleId).HasDatabaseName("ix_sale_items_sale_id");
        });
    }

    private static void ConfigureAuditLogs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.Action).HasColumnName("action").IsRequired().HasMaxLength(100);
            entity.Property(a => a.EntityType).HasColumnName("entity_type").IsRequired().HasMaxLength(100);
            entity.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(100);
            entity.Property(a => a.OldValues).HasColumnName("old_values");
            entity.Property(a => a.NewValues).HasColumnName("new_values");
            entity.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
            entity.Property(a => a.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            entity.Property(a => a.AdditionalInfo).HasColumnName("additional_info").HasMaxLength(1000);
            entity.Property(a => a.Hash).HasColumnName("hash").IsRequired().HasMaxLength(256);
            entity.Property(a => a.PreviousHash).HasColumnName("previous_hash").HasMaxLength(256);
            entity.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            entity.Property(a => a.UpdatedAt).HasColumnName("updated_at");
            entity.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            entity.Property(a => a.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(a => a.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(a => a.EntityType).HasDatabaseName("ix_audit_logs_entity_type");
            entity.HasIndex(a => a.CreatedAt).HasDatabaseName("ix_audit_logs_created_at").IsDescending();
            entity.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("ix_audit_logs_entity");
        });
    }

    private static void ConfigureConsentRecords(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConsentRecord>(entity =>
        {
            entity.ToTable("consent_records");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.SubjectId).HasColumnName("subject_id").IsRequired();
            entity.Property(c => c.SubjectType).HasColumnName("subject_type").IsRequired().HasMaxLength(50);
            entity.Property(c => c.Region).HasColumnName("region").IsRequired().HasMaxLength(50);
            entity.Property(c => c.PrivacyNoticeVersion).HasColumnName("privacy_notice_version").HasMaxLength(20);
            entity.Property(c => c.AcceptedAtUtc).HasColumnName("accepted_at_utc").IsRequired();
            entity.Property(c => c.AcceptedByUserId).HasColumnName("accepted_by_user_id").HasMaxLength(100);
            entity.Property(c => c.EvidenceHash).HasColumnName("evidence_hash").IsRequired().HasMaxLength(128);
            entity.Property(c => c.RevokedAtUtc).HasColumnName("revoked_at_utc");
            entity.Property(c => c.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(500);

            entity.HasIndex(c => c.SubjectId).HasDatabaseName("ix_consent_records_subject_id");
            entity.HasIndex(c => c.AcceptedAtUtc).HasDatabaseName("ix_consent_records_accepted_at");
        });
    }

    private static void ConfigureArcoRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArcoRequest>(entity =>
        {
            entity.ToTable("arco_requests");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.PatientId).HasColumnName("patient_id").IsRequired();
            entity.Property(a => a.RequestType).HasColumnName("request_type").IsRequired().HasMaxLength(50);
            entity.Property(a => a.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
            entity.Property(a => a.DueAtUtc).HasColumnName("due_at_utc").IsRequired();
            entity.Property(a => a.Status).HasColumnName("status").IsRequired().HasMaxLength(30);
            entity.Property(a => a.ResolutionNotes).HasColumnName("resolution_notes");
            entity.Property(a => a.ResolvedAtUtc).HasColumnName("resolved_at_utc");

            entity.HasIndex(a => a.PatientId).HasDatabaseName("ix_arco_requests_patient_id");
            entity.HasIndex(a => a.DueAtUtc).HasDatabaseName("ix_arco_requests_due_at");
            entity.HasIndex(a => a.Status).HasDatabaseName("ix_arco_requests_status");
        });
    }
}
