-- Run in the IAM database only. Seeds dispatch/staffing/leave/consultation permissions for MANAGER and FRONT_DESK_OFFICER.
-- Idempotent: 20261029120000_ManagerStaffingPermissions (STAFFING-01). Do not run concurrently with automatic migration.
START TRANSACTION;
SET LOCAL search_path TO public;
DO $preflight$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL OR to_regclass('public.permissions') IS NULL THEN
        RAISE EXCEPTION 'IAM migration history or permissions table missing. Select the correct database.';
    END IF;
    IF (SELECT count(*) FROM public.roles WHERE role_name IN ('MANAGER','FRONT_DESK_OFFICER')) <> 2 THEN
        RAISE EXCEPTION 'Roles MANAGER and FRONT_DESK_OFFICER must exist before seeding staffing permissions.';
    END IF;
END $preflight$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261029120000_ManagerStaffingPermissions') THEN
    DO $staffing$
    BEGIN
      PERFORM pg_advisory_xact_lock(1464680777);
      LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
      -- Keep the id sequence ahead of rows inserted by hand, otherwise INSERT fails on the primary key.
      PERFORM setval(pg_get_serial_sequence('permissions','id'), GREATEST(COALESCE((SELECT MAX(id) FROM permissions),1), (SELECT last_value FROM permissions_id_seq)), true);
      INSERT INTO permissions(permission_code,permission_name,module) VALUES
        ('workflow.dispatch.read','Xem đề xuất và lịch sử phân công','WORKFLOW'),
        ('workflow.dispatch.write','Phân công và chuyển cán bộ xử lý','WORKFLOW'),
        ('workflow.staffing.read','Xem lịch làm và yêu cầu nghỉ','WORKFLOW'),
        ('workflow.staffing.manage','Quản lý lịch làm và duyệt nghỉ','WORKFLOW'),
        ('workflow.leave.request','Gửi và hủy yêu cầu nghỉ của mình','WORKFLOW'),
        ('workflow.consultation.read','Xem tham vấn được phân quyền','WORKFLOW'),
        ('workflow.consultation.respond','Trả lời tham vấn được giao','WORKFLOW')
      ON CONFLICT(permission_code) DO NOTHING;
      INSERT INTO role_permissions(role_id,permission_id)
      SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
      WHERE (r.role_name = 'MANAGER' AND p.permission_code IN
        ('workflow.dispatch.read','workflow.dispatch.write','workflow.staffing.read','workflow.staffing.manage','workflow.consultation.read'))
        OR (r.role_name = 'FRONT_DESK_OFFICER' AND p.permission_code IN ('workflow.staffing.read','workflow.leave.request','workflow.consultation.read','workflow.consultation.respond'))
      ON CONFLICT(role_id,permission_id) DO NOTHING;
    END $staffing$;
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261029120000_ManagerStaffingPermissions', '8.0.11');
    END IF;
END $EF$;
COMMIT;

-- Users already signed in get the new permissions on their next request after the access-context cache expires (ACCESS-CACHE-01, default 30 s).
