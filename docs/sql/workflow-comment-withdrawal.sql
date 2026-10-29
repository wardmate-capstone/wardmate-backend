-- Run in the Workflow database (public schema) before or during deployment.
-- Idempotent script for 20261029010912_CommentWithdrawal (WORKFLOW-COMMENT-01).
-- Requires 20261029005650_ApplicantNameAndActivity (workflow-applicant-name-activity.sql) first.
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261029005650_ApplicantNameAndActivity') THEN
        RAISE EXCEPTION 'Apply workflow-applicant-name-activity.sql (20261029005650_ApplicantNameAndActivity) first.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261029010912_CommentWithdrawal') THEN
        ALTER TABLE application_comments DROP CONSTRAINT IF EXISTS ck_application_comments_status;
        ALTER TABLE application_comments ADD COLUMN IF NOT EXISTS withdraw_reason character varying(500);
        ALTER TABLE application_comments ADD COLUMN IF NOT EXISTS withdrawn_at timestamp with time zone;
        ALTER TABLE application_comments ADD COLUMN IF NOT EXISTS withdrawn_by uuid;
        ALTER TABLE application_comments ADD CONSTRAINT ck_application_comments_status CHECK (status IN ('OPEN','RESOLVED','WITHDRAWN'));
        ALTER TABLE application_comments DROP CONSTRAINT IF EXISTS ck_application_comments_withdrawal;
        ALTER TABLE application_comments ADD CONSTRAINT ck_application_comments_withdrawal CHECK ((status = 'WITHDRAWN' AND withdrawn_by IS NOT NULL AND withdrawn_at IS NOT NULL AND withdraw_reason IS NOT NULL) OR (status <> 'WITHDRAWN' AND withdrawn_by IS NULL AND withdrawn_at IS NULL AND withdraw_reason IS NULL));
        INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
        VALUES ('20261029010912_CommentWithdrawal', '8.0.11');
    END IF;
END $EF$;

COMMIT;

-- Existing comments stay OPEN/RESOLVED with NULL withdrawal columns, which satisfies the new constraint.
