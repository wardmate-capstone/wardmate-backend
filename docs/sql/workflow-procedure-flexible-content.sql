START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110032213_ProcedureFlexibleContent') THEN
    ALTER TABLE applications ALTER COLUMN procedure_title TYPE character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110032213_ProcedureFlexibleContent') THEN
    ALTER TABLE application_comments ALTER COLUMN field_label TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110032213_ProcedureFlexibleContent') THEN
    ALTER TABLE application_checklists ALTER COLUMN title TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110032213_ProcedureFlexibleContent') THEN
    ALTER TABLE application_checklists ADD form_links jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110032213_ProcedureFlexibleContent') THEN
    ALTER TABLE application_checklists ADD item_note text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110032213_ProcedureFlexibleContent') THEN
    ALTER TABLE application_checklists ADD quantity_text text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261110032213_ProcedureFlexibleContent') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261110032213_ProcedureFlexibleContent', '8.0.11');
    END IF;
END $EF$;
COMMIT;

