START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    ALTER TABLE calendar_closures ADD cancelled_by_name character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    ALTER TABLE calendar_closures ADD created_by_name character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    ALTER TABLE calendar_closures ADD ward_name character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    ALTER TABLE application_consultations ADD application_code character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    ALTER TABLE application_consultations ADD closed_by_name character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    ALTER TABLE application_consultations ADD requested_by_name character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    ALTER TABLE application_consultations ADD specialist_name character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    ALTER TABLE application_assignment_history ADD actor_name character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    ALTER TABLE application_assignment_history ADD from_officer_name character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    ALTER TABLE application_assignment_history ADD to_officer_name character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261108142645_DisplayNameSnapshots') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261108142645_DisplayNameSnapshots', '8.0.11');
    END IF;
END $EF$;
COMMIT;

