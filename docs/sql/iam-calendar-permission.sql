-- Run only on IAM database, after backup. No secrets in this script.
BEGIN;
DO $$ BEGIN
 IF to_regclass('public.roles') IS NULL OR to_regclass('public.permissions') IS NULL OR to_regclass('public.role_permissions') IS NULL OR to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
 RAISE EXCEPTION 'Wrong database: expected migrated IAM schema'; END IF;
 IF NOT EXISTS(SELECT 1 FROM roles WHERE role_name='IT_ADMIN') THEN RAISE EXCEPTION 'IT_ADMIN role is missing'; END IF;
END $$;

        SELECT pg_advisory_xact_lock(1464680777);
        LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
        SELECT setval(pg_get_serial_sequence('permissions', 'id'), GREATEST(COALESCE((SELECT MAX(id) FROM permissions), 1), (SELECT last_value FROM permissions_id_seq)), true);
        INSERT INTO permissions(permission_code, permission_name, module)
        VALUES ('workflow.calendar.manage','Quản lý ngày lễ toàn quốc','WORKFLOW')
        ON CONFLICT (permission_code) DO NOTHING;
        INSERT INTO role_permissions(role_id, permission_id)
        SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
        WHERE r.role_name='IT_ADMIN' AND p.permission_code='workflow.calendar.manage'
        ON CONFLICT (role_id,permission_id) DO NOTHING;
        
INSERT INTO "__EFMigrationsHistory" (migration_id,product_version) VALUES ('20261101060000_WorkflowCalendarPermission','8.0.11') ON CONFLICT DO NOTHING;
COMMIT;
