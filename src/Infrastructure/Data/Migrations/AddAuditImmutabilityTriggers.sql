-- Triggers de inmutabilidad para tablas de auditoría
-- Previene UPDATE y DELETE en tablas de auditoría para garantizar integridad forense

-- Función de trigger genérica para inmutabilidad
CREATE OR REPLACE FUNCTION audit_tables_immutable()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Tabla de auditoría es INMUTABLE. La operación % no está permitida en la tabla %.', TG_OP, TG_TABLE_NAME;
END;
$$ LANGUAGE plpgsql;

-- Trigger para audit_logs
DROP TRIGGER IF EXISTS trg_audit_logs_immutable ON audit_logs;
CREATE TRIGGER trg_audit_logs_immutable
BEFORE UPDATE OR DELETE ON audit_logs
FOR EACH ROW EXECUTE FUNCTION audit_tables_immutable();

-- Trigger para access_logs
DROP TRIGGER IF EXISTS trg_access_logs_immutable ON access_logs;
CREATE TRIGGER trg_access_logs_immutable
BEFORE UPDATE OR DELETE ON access_logs
FOR EACH ROW EXECUTE FUNCTION audit_tables_immutable();

-- Trigger para configuration_changes (si existe)
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'configuration_changes') THEN
        DROP TRIGGER IF EXISTS trg_configuration_changes_immutable ON configuration_changes;
        CREATE TRIGGER trg_configuration_changes_immutable
        BEFORE UPDATE OR DELETE ON configuration_changes
        FOR EACH ROW EXECUTE FUNCTION audit_tables_immutable();
    END IF;
END $$;

-- Trigger para backup_events (si existe)
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'backup_events') THEN
        DROP TRIGGER IF EXISTS trg_backup_events_immutable ON backup_events;
        CREATE TRIGGER trg_backup_events_immutable
        BEFORE UPDATE OR DELETE ON backup_events
        FOR EACH ROW EXECUTE FUNCTION audit_tables_immutable();
    END IF;
END $$;

-- Comentario explicativo
COMMENT ON FUNCTION audit_tables_immutable() IS 'Función de trigger que previene modificaciones en tablas de auditoría. Lanza excepción si se intenta UPDATE o DELETE.';
COMMENT ON TRIGGER trg_audit_logs_immutable ON audit_logs IS 'Previene UPDATE y DELETE en audit_logs para garantizar inmutabilidad forense.';
COMMENT ON TRIGGER trg_access_logs_immutable ON access_logs IS 'Previene UPDATE y DELETE en access_logs para garantizar inmutabilidad forense.';
