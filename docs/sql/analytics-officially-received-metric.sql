START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM analytics."__AnalyticsMigrationsHistory" WHERE "MigrationId" = '20261128030430_OfficiallyReceivedMetric') THEN
    ALTER TABLE analytics.application_procedure_metrics ADD "OfficiallyReceivedCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM analytics."__AnalyticsMigrationsHistory" WHERE "MigrationId" = '20261128030430_OfficiallyReceivedMetric') THEN
    INSERT INTO analytics."__AnalyticsMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261128030430_OfficiallyReceivedMetric', '8.0.11');
    END IF;
END $EF$;
COMMIT;

