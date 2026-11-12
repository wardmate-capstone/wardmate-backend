START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261112161946_PushDevices') THEN
    CREATE TABLE push_devices (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        token character varying(1024) NOT NULL,
        platform character varying(10) NOT NULL,
        app_version character varying(50),
        created_at timestamp with time zone NOT NULL,
        last_seen_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_push_devices" PRIMARY KEY (id),
        CONSTRAINT ck_push_devices_platform CHECK (platform IN ('ANDROID','IOS','WEB'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261112161946_PushDevices') THEN
    CREATE UNIQUE INDEX "IX_push_devices_token" ON push_devices (token);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261112161946_PushDevices') THEN
    CREATE INDEX "IX_push_devices_user_id_last_seen_at" ON push_devices (user_id, last_seen_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261112161946_PushDevices') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261112161946_PushDevices', '8.0.11');
    END IF;
END $EF$;
COMMIT;

