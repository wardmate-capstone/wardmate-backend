START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM analytics."__AnalyticsMigrationsHistory" WHERE "MigrationId" = '20261110032236_FeedbackProcedureTitle1000') THEN
    ALTER TABLE analytics.citizen_feedback ALTER COLUMN "ProcedureTitle" TYPE character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM analytics."__AnalyticsMigrationsHistory" WHERE "MigrationId" = '20261110032236_FeedbackProcedureTitle1000') THEN
    INSERT INTO analytics."__AnalyticsMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110032236_FeedbackProcedureTitle1000', '8.0.11');
    END IF;
END $EF$;
COMMIT;

