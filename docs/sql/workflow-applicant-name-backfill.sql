-- FE-REQ BE-D (2026-11-08): fill applications.applicant_name for applications submitted before
-- 20261029005650_ApplicantNameAndActivity. Database-per-service: Workflow never reads the IAM database,
-- so an operator runs this in two steps. Only the full name is copied (never CCCD, phone or email).
-- Idempotent: only rows whose applicant_name is still NULL are touched.

-- =====================================================================================
-- STEP 1 — run in the **IAM** database. It returns one UPDATE statement per citizen who has
-- a profile name. Copy the whole "stmt" column (all rows) into STEP 2. Do not save the output
-- in the repository or in chat: it contains personal names.
-- =====================================================================================
SELECT format(
    'UPDATE applications SET applicant_name = %L WHERE user_id = %L::uuid AND applicant_name IS NULL AND submitted_at IS NOT NULL;',
    left(btrim(p.full_name), 255), p.user_id) AS stmt
FROM user_profiles p
WHERE p.full_name IS NOT NULL AND btrim(p.full_name) <> '';

-- =====================================================================================
-- STEP 2 — run in the **Workflow** database, inside one transaction:
--   BEGIN;
--   <paste every statement produced by STEP 1>
--   COMMIT;
-- Check afterwards (expect only citizens without an IAM profile name to remain):
--   SELECT count(*) FROM applications WHERE submitted_at IS NOT NULL AND applicant_name IS NULL;
-- =====================================================================================

-- Feedback rows created before 20261108142818_FeedbackDisplaySnapshots keep ApplicationCode,
-- ProcedureTitle and ApplicantName NULL; FE falls back to reading the application detail.
