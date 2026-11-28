START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261128030419_QrApplicationCode') THEN
    ALTER TABLE document.approved_qrs ADD application_code character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261128030419_QrApplicationCode') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261128030419_QrApplicationCode', '8.0.11');
    END IF;
END $EF$;
COMMIT;

