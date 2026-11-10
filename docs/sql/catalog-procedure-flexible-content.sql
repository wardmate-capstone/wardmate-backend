START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    ALTER TABLE procedures ALTER COLUMN title TYPE character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    ALTER TABLE procedures ALTER COLUMN target_audience TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    ALTER TABLE procedures ALTER COLUMN processing_time_summary TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    ALTER TABLE procedures ALTER COLUMN procedure_code TYPE character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    ALTER TABLE procedures ALTER COLUMN level_of_implementation TYPE character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    ALTER TABLE procedures ALTER COLUMN issuing_authority TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    ALTER TABLE procedures ALTER COLUMN fee_summary TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    ALTER TABLE procedures ADD search_keywords text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    ALTER TABLE procedure_versions ALTER COLUMN decision_number TYPE character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    UPDATE procedures SET search_keywords = NULL
    WHERE id = '07000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110031929_ProcedureFlexibleContent') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261110031929_ProcedureFlexibleContent', '8.0.11');
    END IF;
END $EF$;
COMMIT;

