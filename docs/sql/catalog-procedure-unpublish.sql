START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112062426_ProcedureUnpublish') THEN
    ALTER TABLE procedures ADD first_published_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112062426_ProcedureUnpublish') THEN
    UPDATE procedures SET first_published_at = COALESCE(updated_at, created_at, now()) WHERE first_published_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112062426_ProcedureUnpublish') THEN
    UPDATE procedures SET first_published_at = TIMESTAMPTZ '2026-09-30T00:00:00Z'
    WHERE id = '07000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112062426_ProcedureUnpublish') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261112062426_ProcedureUnpublish', '8.0.11');
    END IF;
END $EF$;
COMMIT;

