START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261128030425_ApplicationCaseName') THEN
    ALTER TABLE applications ADD case_name character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261128030425_ApplicationCaseName') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261128030425_ApplicationCaseName', '8.0.11');
    END IF;
END $EF$;
COMMIT;

