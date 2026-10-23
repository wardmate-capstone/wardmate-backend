START TRANSACTION;

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
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261023150528_EsmsNotificationDelivery') INTO v_has_migration;
    ELSE
        v_is_snake := false;
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023150528_EsmsNotificationDelivery') INTO v_has_migration;
    END IF;

    IF NOT v_has_migration THEN
        ALTER TABLE notification_deliveries ADD COLUMN IF NOT EXISTS external_message_id character varying(100);
        ALTER TABLE notification_deliveries ADD COLUMN IF NOT EXISTS is_sandbox boolean;
        ALTER TABLE notification_deliveries ADD COLUMN IF NOT EXISTS provider character varying(30);
        CREATE INDEX IF NOT EXISTS "IX_notification_deliveries_provider_external_message_id" ON notification_deliveries (provider, external_message_id);

        IF v_is_snake THEN
            INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
            VALUES ('20261023150528_EsmsNotificationDelivery', '8.0.11');
        ELSE
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20261023150528_EsmsNotificationDelivery', '8.0.11');
        END IF;
    END IF;
END $EF$;

COMMIT;
