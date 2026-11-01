START TRANSACTION;
SELECT pg_advisory_xact_lock(1464680778);
-- Workflow database only; apply the preceding migrations first. Review/backup before production.
-- Never adopt an unknown existing table as an applied migration.
DO $$ BEGIN
 IF to_regclass('public.applications') IS NULL OR to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
 RAISE EXCEPTION 'Wrong database: expected migrated Workflow schema'; END IF;
 IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id='20261029161554_AddStaffingTablesOnly') THEN
 RAISE EXCEPTION 'Required preceding Workflow migration is missing'; END IF;
 IF to_regclass('public.calendar_closures') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id='20261101055515_CalendarClosures') THEN
 RAISE EXCEPTION 'Untracked calendar_closures table: compare schema manually, do not mark as applied'; END IF;
END $$;



DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261101055515_CalendarClosures') THEN
    CREATE TABLE calendar_closures (
        id uuid NOT NULL,
        ward_code character varying(50),
        closure_date date NOT NULL,
        effective_from_utc timestamp with time zone NOT NULL,
        reason character varying(500) NOT NULL,
        created_by uuid NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        cancelled_by uuid,
        cancelled_at_utc timestamp with time zone,
        cancellation_reason character varying(500),
        CONSTRAINT pk_calendar_closures PRIMARY KEY (id),
        CONSTRAINT ck_closure_cancellation CHECK ((cancelled_by IS NULL AND cancelled_at_utc IS NULL AND cancellation_reason IS NULL) OR (cancelled_by IS NOT NULL AND cancelled_at_utc IS NOT NULL AND cancellation_reason IS NOT NULL)),
        CONSTRAINT ck_closure_reason CHECK (length(btrim(reason)) BETWEEN 1 AND 500)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261101055515_CalendarClosures') THEN
    CREATE INDEX ix_calendar_closures_closure_date_id ON calendar_closures (closure_date, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261101055515_CalendarClosures') THEN
    CREATE UNIQUE INDEX ix_calendar_closures_ward_code_closure_date ON calendar_closures (ward_code, closure_date) WHERE cancelled_at_utc IS NULL AND ward_code IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261101055515_CalendarClosures') THEN
    CREATE UNIQUE INDEX ux_calendar_closures_national ON calendar_closures (closure_date) WHERE cancelled_at_utc IS NULL AND ward_code IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261101055515_CalendarClosures') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261101055515_CalendarClosures', '8.0.11');
    END IF;
END $EF$;
COMMIT;

