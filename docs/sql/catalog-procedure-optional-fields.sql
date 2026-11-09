START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261109093503_ProcedureOptionalFields') THEN
    ALTER TABLE procedures ALTER COLUMN target_audience DROP NOT NULL;
    ALTER TABLE procedures ALTER COLUMN target_audience DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261109093503_ProcedureOptionalFields') THEN
    ALTER TABLE procedures ALTER COLUMN processing_time_summary DROP NOT NULL;
    ALTER TABLE procedures ALTER COLUMN processing_time_summary DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261109093503_ProcedureOptionalFields') THEN
    ALTER TABLE procedures ALTER COLUMN level_of_implementation DROP NOT NULL;
    ALTER TABLE procedures ALTER COLUMN level_of_implementation DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261109093503_ProcedureOptionalFields') THEN
    ALTER TABLE procedures ALTER COLUMN fee_summary DROP NOT NULL;
    ALTER TABLE procedures ALTER COLUMN fee_summary DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261109093503_ProcedureOptionalFields') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261109093503_ProcedureOptionalFields', '8.0.11');
    END IF;
END $EF$;
COMMIT;

