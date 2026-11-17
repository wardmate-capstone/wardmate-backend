START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117144843_SubmissionFileVersions') THEN
    CREATE TABLE document.submission_file_versions (
        id uuid NOT NULL,
        application_id uuid NOT NULL,
        checklist_id uuid,
        source_type character varying(30) NOT NULL,
        source_id uuid NOT NULL,
        application_version_number integer NOT NULL,
        blob_url text NOT NULL,
        file_name character varying(255) NOT NULL,
        file_size_bytes bigint NOT NULL,
        content_type character varying(255) NOT NULL,
        sha256 character varying(64) NOT NULL,
        form_data jsonb,
        source_updated_at_utc timestamp with time zone NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_submission_file_versions PRIMARY KEY (id),
        CONSTRAINT ck_submission_file_versions_source CHECK (source_type IN ('SUBMISSION','SUPPORTING_DOCUMENT')),
        CONSTRAINT ck_submission_file_versions_version CHECK (application_version_number >= 1)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117144843_SubmissionFileVersions') THEN
    CREATE UNIQUE INDEX ux_submission_file_versions_source ON document.submission_file_versions (application_id, application_version_number, source_type, source_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117144843_SubmissionFileVersions') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261117144843_SubmissionFileVersions', '8.0.11');
    END IF;
END $EF$;
COMMIT;

