START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023150528_EsmsNotificationDelivery') THEN
    ALTER TABLE notification_deliveries ADD external_message_id character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023150528_EsmsNotificationDelivery') THEN
    ALTER TABLE notification_deliveries ADD is_sandbox boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023150528_EsmsNotificationDelivery') THEN
    ALTER TABLE notification_deliveries ADD provider character varying(30);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023150528_EsmsNotificationDelivery') THEN
    CREATE INDEX "IX_notification_deliveries_provider_external_message_id" ON notification_deliveries (provider, external_message_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023150528_EsmsNotificationDelivery') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023150528_EsmsNotificationDelivery', '8.0.11');
    END IF;
END $EF$;
COMMIT;

