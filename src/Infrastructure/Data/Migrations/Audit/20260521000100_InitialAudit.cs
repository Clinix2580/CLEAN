using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OS.Infrastructure.Data.Migrations.Audit;

public partial class InitialAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "access_logs",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                table_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                record_id = table.Column<string>(type: "text", nullable: true),
                accessed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                ip_address = table.Column<string>(type: "text", nullable: true),
                success = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                failure_reason = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_access_logs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "audit_logs",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                table_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                record_id = table.Column<string>(type: "text", nullable: true),
                old_values = table.Column<string>(type: "jsonb", nullable: true),
                new_values = table.Column<string>(type: "jsonb", nullable: true),
                changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                ip_address = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_logs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "backup_events",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                backup_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                error_message = table.Column<string>(type: "text", nullable: true),
                triggered_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                verification_hash = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_backup_events", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "configuration_changes",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                config_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                old_value = table.Column<string>(type: "text", nullable: true),
                new_value = table.Column<string>(type: "text", nullable: true),
                changed_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                reason = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_configuration_changes", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_access_logs_accessed_at",
            table: "access_logs",
            column: "accessed_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_access_logs_table_record",
            table: "access_logs",
            columns: new[] { "table_name", "record_id" });

        migrationBuilder.CreateIndex(
            name: "ix_access_logs_user",
            table: "access_logs",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_audit_logs_changed_at",
            table: "audit_logs",
            column: "changed_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_audit_logs_table",
            table: "audit_logs",
            column: "table_name");

        migrationBuilder.CreateIndex(
            name: "ix_audit_logs_table_record",
            table: "audit_logs",
            columns: new[] { "table_name", "record_id" });

        migrationBuilder.CreateIndex(
            name: "ix_backup_events_started_at",
            table: "backup_events",
            column: "started_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_backup_events_status",
            table: "backup_events",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_config_changes_changed_at",
            table: "configuration_changes",
            column: "changed_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_config_changes_key",
            table: "configuration_changes",
            column: "config_key");

        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION audit_tables_immutable()
            RETURNS TRIGGER AS $$
            BEGIN
                RAISE EXCEPTION 'Tabla de auditoría es INMUTABLE. La operación % no está permitida en la tabla %.', TG_OP, TG_TABLE_NAME;
            END;
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS trg_audit_logs_immutable ON audit_logs;
            CREATE TRIGGER trg_audit_logs_immutable
            BEFORE UPDATE OR DELETE ON audit_logs
            FOR EACH ROW EXECUTE FUNCTION audit_tables_immutable();

            DROP TRIGGER IF EXISTS trg_access_logs_immutable ON access_logs;
            CREATE TRIGGER trg_access_logs_immutable
            BEFORE UPDATE OR DELETE ON access_logs
            FOR EACH ROW EXECUTE FUNCTION audit_tables_immutable();

            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'configuration_changes') THEN
                    DROP TRIGGER IF EXISTS trg_configuration_changes_immutable ON configuration_changes;
                    CREATE TRIGGER trg_configuration_changes_immutable
                    BEFORE UPDATE OR DELETE ON configuration_changes
                    FOR EACH ROW EXECUTE FUNCTION audit_tables_immutable();
                END IF;
            END $$;

            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'backup_events') THEN
                    DROP TRIGGER IF EXISTS trg_backup_events_immutable ON backup_events;
                    CREATE TRIGGER trg_backup_events_immutable
                    BEFORE UPDATE OR DELETE ON backup_events
                    FOR EACH ROW EXECUTE FUNCTION audit_tables_immutable();
                END IF;
            END $$;

            COMMENT ON FUNCTION audit_tables_immutable() IS 'Función de trigger que previene modificaciones en tablas de auditoría. Lanza excepción si se intenta UPDATE o DELETE.';
            COMMENT ON TRIGGER trg_audit_logs_immutable ON audit_logs IS 'Previene UPDATE y DELETE en audit_logs para garantizar inmutabilidad forense.';
            COMMENT ON TRIGGER trg_access_logs_immutable ON access_logs IS 'Previene UPDATE y DELETE en access_logs para garantizar inmutabilidad forense.';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_backup_events_immutable ON backup_events;
            DROP TRIGGER IF EXISTS trg_configuration_changes_immutable ON configuration_changes;
            DROP TRIGGER IF EXISTS trg_access_logs_immutable ON access_logs;
            DROP TRIGGER IF EXISTS trg_audit_logs_immutable ON audit_logs;
            DROP FUNCTION IF EXISTS audit_tables_immutable();
            """);

        migrationBuilder.DropTable(name: "access_logs");
        migrationBuilder.DropTable(name: "audit_logs");
        migrationBuilder.DropTable(name: "backup_events");
        migrationBuilder.DropTable(name: "configuration_changes");
    }
}
