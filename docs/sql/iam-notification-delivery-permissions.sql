-- Run in the IAM database only. Seeds notifications.deliveries.read/manage for IT_ADMIN.
-- Idempotent: 20261024090000_NotificationDeliveryAdministration. Do not run concurrently with automatic migration.
START TRANSACTION;
SET LOCAL search_path TO public;
DO $preflight$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL OR to_regclass('public.permissions') IS NULL THEN
        RAISE EXCEPTION 'IAM migration history or permissions table missing. Select the correct database.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public.roles WHERE role_name = 'IT_ADMIN') THEN
        RAISE EXCEPTION 'Role IT_ADMIN not found; check the IAM database before seeding.';
    END IF;
END $preflight$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024090000_NotificationDeliveryAdministration') THEN
    DO $delivery$
    BEGIN
      PERFORM pg_advisory_xact_lock(1464680777);
      LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
      PERFORM setval(pg_get_serial_sequence('permissions','id'), GREATEST(COALESCE((SELECT MAX(id) FROM permissions),1), (SELECT last_value FROM permissions_id_seq)), true);
      INSERT INTO permissions(permission_code,permission_name,module) VALUES
        ('notifications.deliveries.read','Xem lỗi và lịch sử gửi thông báo','AnalyticsSystem'),
        ('notifications.deliveries.manage','Đối soát và gửi lại thông báo','AnalyticsSystem')
      ON CONFLICT(permission_code) DO NOTHING;
      INSERT INTO role_permissions(role_id,permission_id)
      SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
      WHERE r.role_name='IT_ADMIN' AND p.permission_code IN ('notifications.deliveries.read','notifications.deliveries.manage')
      ON CONFLICT(role_id,permission_id) DO NOTHING;
    END $delivery$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024090000_NotificationDeliveryAdministration') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261024090000_NotificationDeliveryAdministration', '8.0.11');
    END IF;
END $EF$;
COMMIT;

