-- Run in the Notifications database (AnalyticsSystem NotificationDbContext) before deploying the new AnalyticsSystem code.
-- Idempotent: 20261024020118_NotificationDeliveryAdministration. Do not run concurrently with automatic migration.
START TRANSACTION;
SET LOCAL search_path TO public;

DO $preflight$
DECLARE
    v_has_baseline boolean := false;
BEGIN
    IF to_regclass('public.notification_deliveries') IS NULL OR to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'notification_deliveries or migration history missing. Select the Notifications database.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = '__EFMigrationsHistory' AND column_name = 'migration_id'
    ) THEN
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261023150528_EsmsNotificationDelivery') INTO v_has_baseline;
    ELSE
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023150528_EsmsNotificationDelivery') INTO v_has_baseline;
    END IF;

    IF NOT v_has_baseline THEN
        RAISE EXCEPTION 'Baseline 20261023150528_EsmsNotificationDelivery is required. Run notification-esms-delivery.sql first.';
    END IF;
END $preflight$;

DO $EF$
DECLARE
    v_has_migration boolean := false;
    v_is_snake boolean := true;
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = '__EFMigrationsHistory' AND column_name = 'migration_id'
    ) THEN
        v_is_snake := true;
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261024020118_NotificationDeliveryAdministration') INTO v_has_migration;
    ELSE
        v_is_snake := false;
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024020118_NotificationDeliveryAdministration') INTO v_has_migration;
    END IF;

    IF NOT v_has_migration THEN
        ALTER TABLE notification_deliveries ADD COLUMN IF NOT EXISTS revision uuid NOT NULL DEFAULT (gen_random_uuid());
        ALTER TABLE notification_deliveries ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone NOT NULL DEFAULT (now());

        CREATE TABLE IF NOT EXISTS notification_delivery_audits (
            "Id" uuid NOT NULL,
            "DeliveryId" uuid NOT NULL,
            "ActorId" uuid NOT NULL,
            "Action" character varying(40) NOT NULL,
            "BeforeState" character varying(30) NOT NULL,
            "AfterState" character varying(30) NOT NULL,
            "BeforeAttempts" integer NOT NULL,
            "BeforeRevision" uuid NOT NULL,
            "AfterRevision" uuid NOT NULL,
            "Reason" character varying(500) NOT NULL,
            "Reference" character varying(200),
            "BeforeMessageId" character varying(100),
            "AfterMessageId" character varying(100),
            "CreatedAt" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_notification_delivery_audits" PRIMARY KEY ("Id")
        );

        CREATE INDEX IF NOT EXISTS "IX_notification_deliveries_State_updated_at" ON notification_deliveries ("State", updated_at);
        CREATE INDEX IF NOT EXISTS "IX_notification_delivery_audits_DeliveryId_CreatedAt" ON notification_delivery_audits ("DeliveryId", "CreatedAt");

        IF v_is_snake THEN
            INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
            VALUES ('20261024020118_NotificationDeliveryAdministration', '8.0.11');
        ELSE
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20261024020118_NotificationDeliveryAdministration', '8.0.11');
        END IF;
    END IF;
END $EF$;

COMMIT;
