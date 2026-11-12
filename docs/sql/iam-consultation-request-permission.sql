-- Run in the IAM database only. Seeds workflow.consultation.request permission for FRONT_DESK_OFFICER.
-- Idempotent: 20261112080000_OfficerConsultationRequestPermission (STAFFING-01 consultation dispatch).
-- Requires: iam-staffing-permissions.sql (20261029120000_ManagerStaffingPermissions) to have been applied first.
START TRANSACTION;
SET LOCAL search_path TO public;

DO $preflight$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL OR to_regclass('public.permissions') IS NULL THEN
        RAISE EXCEPTION 'IAM migration history or permissions table missing. Select the correct database.';
    END IF;
    IF (SELECT count(*) FROM public.roles WHERE role_name = 'FRONT_DESK_OFFICER') = 0 THEN
        RAISE EXCEPTION 'Role FRONT_DESK_OFFICER must exist before seeding consultation.request permission.';
    END IF;
END $preflight$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112080000_OfficerConsultationRequestPermission') THEN
    DO $perm$
    BEGIN
      PERFORM pg_advisory_xact_lock(1464680777);
      LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
      PERFORM setval(pg_get_serial_sequence('permissions','id'), GREATEST(COALESCE((SELECT MAX(id) FROM permissions),1), (SELECT last_value FROM permissions_id_seq)), true);

      INSERT INTO permissions(permission_code, permission_name, module) VALUES
        ('workflow.consultation.request','Yêu cầu tham vấn cho hồ sơ đang phụ trách','WORKFLOW')
      ON CONFLICT(permission_code) DO NOTHING;

      INSERT INTO role_permissions(role_id, permission_id)
      SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
      WHERE r.role_name = 'FRONT_DESK_OFFICER' AND p.permission_code = 'workflow.consultation.request'
      ON CONFLICT(role_id, permission_id) DO NOTHING;

    END $perm$;

    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261112080000_OfficerConsultationRequestPermission', '8.0.11');
    END IF;
END $EF$;

COMMIT;
-- Users already signed in get the new permission on their next request after the access-context cache expires (default 30 s).
