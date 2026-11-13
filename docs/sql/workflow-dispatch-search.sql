START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261113125503_DispatchSearch') THEN
    CREATE EXTENSION IF NOT EXISTS unaccent;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261113125503_DispatchSearch') THEN
    CREATE INDEX ix_applications_ward_code_created_at_id ON applications (ward_code, created_at, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261113125503_DispatchSearch') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261113125503_DispatchSearch', '8.0.11');
    END IF;
END $EF$;
COMMIT;

