-- Run in the Workflow database (public schema) before or during deployment.
-- Idempotent script for 20261029005650_ApplicantNameAndActivity (WORKFLOW-FE-1026).
-- Requires 20261023173500_CategoryAssignment (application_assignment_history) first.
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261023173500_CategoryAssignment') THEN
        RAISE EXCEPTION 'Apply workflow-category-assignment.sql (20261023173500_CategoryAssignment) first.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261029005650_ApplicantNameAndActivity') THEN
        ALTER TABLE applications ADD COLUMN IF NOT EXISTS applicant_name character varying(255);
        CREATE INDEX IF NOT EXISTS ix_application_status_history_changed_by_created_at ON application_status_history (changed_by, created_at);
        CREATE INDEX IF NOT EXISTS ix_application_assignment_history_actor_id_created_at ON application_assignment_history (actor_id, created_at);
        INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
        VALUES ('20261029005650_ApplicantNameAndActivity', '8.0.11');
    END IF;
END $EF$;

COMMIT;

-- Existing applications keep applicant_name NULL (FE shows "Công dân"). The name is filled from the IAM
-- profile on the next submit/resubmit. Workflow must not read the IAM database; an optional backfill
-- needs an operator-run UPDATE from an approved IAM export (user_id -> full_name), never CCCD/phone.
