-- Run in the IAM database only, before deploying the new IAM code.
-- Do not run concurrently with automatic migration.
START TRANSACTION;
SET LOCAL search_path TO public;
DO $preflight$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'IAM migration history missing. Select the correct database.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE migration_id = '20261022063951_AddUserOtpsTable') THEN
        RAISE EXCEPTION 'IAM baseline 20261022063951_AddUserOtpsTable is required.';
    END IF;
END $preflight$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022170202_AccountSecurityHardening') THEN
    ALTER TABLE iam.user_otps DROP CONSTRAINT ck_user_otps_purpose;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022170202_AccountSecurityHardening') THEN
    ALTER TABLE users ADD security_stamp uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022170202_AccountSecurityHardening') THEN
    ALTER TABLE users ADD verified_phone_number text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022170202_AccountSecurityHardening') THEN
    ALTER TABLE iam.user_otps ADD target_phone_number character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022170202_AccountSecurityHardening') THEN
    ALTER TABLE iam.user_otps ADD CONSTRAINT ck_user_otps_purpose CHECK (purpose IN ('CHANGE_PASSWORD','FORGOT_PASSWORD','VERIFY_PHONE'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022170202_AccountSecurityHardening') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261022170202_AccountSecurityHardening', '8.0.11');
    END IF;
END $EF$;
COMMIT;

