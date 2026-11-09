START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261109073630_ApprovedQrOptionalSubmission') THEN
    ALTER TABLE document.approved_qrs ALTER COLUMN submission_id DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261109073630_ApprovedQrOptionalSubmission') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261109073630_ApprovedQrOptionalSubmission', '8.0.11');
    END IF;
END $EF$;
COMMIT;

