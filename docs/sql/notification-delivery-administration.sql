-- Run in the Notifications database (AnalyticsSystem NotificationDbContext) before deploying the new AnalyticsSystem code.
-- Idempotent: 20261024020118_NotificationDeliveryAdministration. Do not run concurrently with automatic migration.
START TRANSACTION;
SET LOCAL search_path TO public;
DO $preflight$
BEGIN
    IF to_regclass('public.notification_deliveries') IS NULL OR to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'notification_deliveries or migration history missing. Select the Notifications database.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261023150528_EsmsNotificationDelivery') THEN
        RAISE EXCEPTION 'Baseline 20261023150528_EsmsNotificationDelivery is required. Run notification-esms-delivery.sql first.';
    END IF;
END $preflight$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024020118_NotificationDeliveryAdministration') THEN
    ALTER TABLE notification_deliveries ADD revision uuid NOT NULL DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024020118_NotificationDeliveryAdministration') THEN
    ALTER TABLE notification_deliveries ADD updated_at timestamp with time zone NOT NULL DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024020118_NotificationDeliveryAdministration') THEN
    CREATE TABLE notification_delivery_audits (
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024020118_NotificationDeliveryAdministration') THEN
    CREATE INDEX "IX_notification_deliveries_State_updated_at" ON notification_deliveries ("State", updated_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024020118_NotificationDeliveryAdministration') THEN
    CREATE INDEX "IX_notification_delivery_audits_DeliveryId_CreatedAt" ON notification_delivery_audits ("DeliveryId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024020118_NotificationDeliveryAdministration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261024020118_NotificationDeliveryAdministration', '8.0.11');
    END IF;
END $EF$;
COMMIT;

