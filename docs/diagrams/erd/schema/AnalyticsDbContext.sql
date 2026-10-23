DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'analytics') THEN
        CREATE SCHEMA analytics;
    END IF;
END $EF$;


CREATE TABLE analytics.application_procedure_metrics (
    "MetricDate" date NOT NULL,
    "ProcedureId" uuid NOT NULL,
    "WardCode" character varying(50) NOT NULL,
    "ReceivedCount" integer NOT NULL,
    "ReadyCount" integer NOT NULL,
    "RevisionRequestedCount" integer NOT NULL,
    "ResubmittedCount" integer NOT NULL,
    "CancelledCount" integer NOT NULL,
    "LegacyApprovedCount" integer NOT NULL,
    "LegacyRejectedCount" integer NOT NULL,
    CONSTRAINT "PK_application_procedure_metrics" PRIMARY KEY ("MetricDate", "WardCode", "ProcedureId")
);


CREATE TABLE analytics.citizen_feedback (
    "Id" uuid NOT NULL,
    "ApplicationId" uuid NOT NULL,
    "CitizenId" uuid NOT NULL,
    "ProcedureId" uuid NOT NULL,
    "WardCode" character varying(50) NOT NULL,
    "Rating" integer NOT NULL,
    "Comment" character varying(2000),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_citizen_feedback" PRIMARY KEY ("Id"),
    CONSTRAINT ck_feedback_rating CHECK ("Rating" BETWEEN 1 AND 5)
);


CREATE TABLE analytics.daily_performance_metrics (
    "MetricDate" date NOT NULL,
    "OfficerId" uuid NOT NULL,
    "ProcedureId" uuid NOT NULL,
    "WardCode" character varying(50) NOT NULL,
    "ReadyCount" integer NOT NULL,
    "RevisionRequestedCount" integer NOT NULL,
    "DurationSampleCount" integer NOT NULL,
    "TotalProcessingMinutes" double precision NOT NULL,
    CONSTRAINT "PK_daily_performance_metrics" PRIMARY KEY ("MetricDate", "WardCode", "OfficerId", "ProcedureId")
);


CREATE TABLE analytics.metric_refreshes (
    "MetricDate" date NOT NULL,
    "SourceAtUtc" timestamp with time zone NOT NULL,
    "RefreshedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_metric_refreshes" PRIMARY KEY ("MetricDate")
);


CREATE INDEX "IX_application_procedure_metrics_WardCode_MetricDate" ON analytics.application_procedure_metrics ("WardCode", "MetricDate");


CREATE UNIQUE INDEX "IX_citizen_feedback_ApplicationId" ON analytics.citizen_feedback ("ApplicationId");


CREATE INDEX "IX_citizen_feedback_CitizenId_ApplicationId" ON analytics.citizen_feedback ("CitizenId", "ApplicationId");


CREATE INDEX "IX_citizen_feedback_WardCode_CreatedAtUtc" ON analytics.citizen_feedback ("WardCode", "CreatedAtUtc");


CREATE INDEX "IX_daily_performance_metrics_WardCode_MetricDate" ON analytics.daily_performance_metrics ("WardCode", "MetricDate");


