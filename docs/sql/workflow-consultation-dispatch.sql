-- Run in the Workflow database (public schema; shared PostgreSQL with DocumentForm) before deploying the new Workflow image.
-- Idempotent script for 20261112085434_OfficerConsultationDispatch (STAFFING-01 consultation dispatch).
-- Requires: workflow-staffing-tables.sql (20261029161554_AddStaffingTablesOnly) to have been applied first.
START TRANSACTION;

DO $EF$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL OR to_regclass('public.application_consultations') IS NULL THEN
        RAISE EXCEPTION 'Workflow migration history or application_consultations table missing. Apply workflow-staffing-tables.sql first.';
    END IF;
END $EF$;

DO $migrate$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112085434_OfficerConsultationDispatch') THEN

        -- 1. Drop old check constraint (status IN ('OPEN','ANSWERED','CLOSED'))
        ALTER TABLE application_consultations DROP CONSTRAINT IF EXISTS ck_consultation_status;

        -- 2. Make specialist_id nullable
        ALTER TABLE application_consultations ALTER COLUMN specialist_id DROP NOT NULL;

        -- 3. Add new columns
        ALTER TABLE application_consultations ADD COLUMN IF NOT EXISTS decision text;
        ALTER TABLE application_consultations ADD COLUMN IF NOT EXISTS dispatched_at_utc timestamp with time zone;
        ALTER TABLE application_consultations ADD COLUMN IF NOT EXISTS dispatched_by uuid;
        ALTER TABLE application_consultations ADD COLUMN IF NOT EXISTS willing_to_take_over boolean NOT NULL DEFAULT false;

        -- 4. Add new index (unique per application+assignment_version while PENDING_DISPATCH)
        CREATE UNIQUE INDEX IF NOT EXISTS ix_application_consultations_application_id_assignment_version
            ON application_consultations (application_id, assignment_version)
            WHERE status = 'PENDING_DISPATCH';

        -- 5. Add updated check constraint
        ALTER TABLE application_consultations ADD CONSTRAINT ck_consultation_status
            CHECK (status IN ('PENDING_DISPATCH','OPEN','ANSWERED','CLOSED'));

        -- 6. Record migration
        INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
        VALUES ('20261112085434_OfficerConsultationDispatch', '8.0.11')
        ON CONFLICT (migration_id) DO NOTHING;

    END IF;
END $migrate$;

COMMIT;
-- If the table already has rows with status 'OPEN'/'ANSWERED'/'CLOSED', the new constraint still allows them.
-- New status 'PENDING_DISPATCH' will be used for unassigned consultation requests.
