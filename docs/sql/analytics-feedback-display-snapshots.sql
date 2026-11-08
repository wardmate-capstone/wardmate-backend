START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM analytics."__AnalyticsMigrationsHistory" WHERE "MigrationId" = '20261108142818_FeedbackDisplaySnapshots') THEN
    ALTER TABLE analytics.citizen_feedback ADD "ApplicantName" character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM analytics."__AnalyticsMigrationsHistory" WHERE "MigrationId" = '20261108142818_FeedbackDisplaySnapshots') THEN
    ALTER TABLE analytics.citizen_feedback ADD "ApplicationCode" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM analytics."__AnalyticsMigrationsHistory" WHERE "MigrationId" = '20261108142818_FeedbackDisplaySnapshots') THEN
    ALTER TABLE analytics.citizen_feedback ADD "ProcedureTitle" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM analytics."__AnalyticsMigrationsHistory" WHERE "MigrationId" = '20261108142818_FeedbackDisplaySnapshots') THEN
    INSERT INTO analytics."__AnalyticsMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108142818_FeedbackDisplaySnapshots', '8.0.11');
    END IF;
END $EF$;
COMMIT;

