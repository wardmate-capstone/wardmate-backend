DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'document') THEN
        CREATE SCHEMA document;
    END IF;
END $EF$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'document') THEN
        CREATE SCHEMA document;
    END IF;
END $EF$;


CREATE EXTENSION IF NOT EXISTS "uuid-ossp";


CREATE TABLE document.application_forms (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    application_id uuid NOT NULL,
    form_data jsonb NOT NULL,
    created_by character varying(100),
    updated_by character varying(100),
    created_at timestamptz NOT NULL,
    updated_at timestamptz,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    deleted_at timestamptz,
    CONSTRAINT pk_application_forms PRIMARY KEY (id)
);


CREATE TABLE document.approved_qrs (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    application_id uuid NOT NULL,
    submission_id uuid NOT NULL,
    qr_code_value character varying(2048) NOT NULL,
    qr_image_png bytea NOT NULL,
    generated_at timestamptz NOT NULL,
    CONSTRAINT pk_approved_qrs PRIMARY KEY (id)
);


CREATE TABLE document.form_templates (
    id uuid NOT NULL,
    code character varying(50) NOT NULL,
    title character varying(255) NOT NULL,
    file_docx_url text,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_by character varying(100),
    updated_by character varying(100),
    created_at timestamptz NOT NULL,
    updated_at timestamptz,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    deleted_at timestamptz,
    CONSTRAINT pk_form_templates PRIMARY KEY (id)
);


CREATE TABLE document.generated_documents (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    application_id uuid NOT NULL,
    submission_id uuid,
    document_type character varying(50) NOT NULL,
    pdf_blob_url text NOT NULL,
    created_by character varying(100),
    updated_by character varying(100),
    created_at timestamptz NOT NULL,
    updated_at timestamptz,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    deleted_at timestamptz,
    CONSTRAINT pk_generated_documents PRIMARY KEY (id)
);


CREATE TABLE document.supporting_documents (
    id uuid NOT NULL,
    application_id uuid NOT NULL,
    submission_type character varying(30),
    document_copy_type character varying(30),
    quantity integer NOT NULL DEFAULT 1,
    checklist_id uuid,
    file_name character varying(255) NOT NULL,
    blob_url text NOT NULL,
    file_size_bytes bigint,
    content_type character varying(100),
    created_by character varying(100),
    updated_by character varying(100),
    created_at timestamptz NOT NULL,
    updated_at timestamptz,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    deleted_at timestamptz,
    CONSTRAINT pk_supporting_documents PRIMARY KEY (id)
);


CREATE TABLE document.form_template_versions (
    id uuid NOT NULL,
    original_blob_url character varying(2048) NOT NULL,
    original_sha256 character varying(64) NOT NULL,
    mapping_definition jsonb NOT NULL,
    template_id uuid NOT NULL,
    version_number integer NOT NULL,
    schema_definition jsonb NOT NULL,
    created_by text,
    updated_by text,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone,
    is_deleted boolean NOT NULL,
    deleted_at_utc timestamp with time zone,
    CONSTRAINT pk_form_template_versions PRIMARY KEY (id),
    CONSTRAINT fk_form_template_versions_form_templates_template_id FOREIGN KEY (template_id) REFERENCES document.form_templates (id) ON DELETE RESTRICT
);


CREATE TABLE document.user_submissions (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    template_version_id uuid,
    form_data jsonb,
    template_id uuid NOT NULL,
    application_id uuid,
    checklist_id uuid,
    applicant_id uuid NOT NULL,
    blob_url character varying(2048) NOT NULL,
    file_name character varying(512) NOT NULL,
    file_size_bytes bigint NOT NULL,
    status character varying(50) NOT NULL,
    officer_comment character varying(2000),
    reviewed_by_officer_id uuid,
    submitted_at timestamptz,
    reviewed_at timestamptz,
    created_by character varying(100),
    updated_by character varying(100),
    created_at timestamptz NOT NULL,
    updated_at timestamptz,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    deleted_at timestamptz,
    CONSTRAINT pk_user_submissions PRIMARY KEY (id),
    CONSTRAINT fk_user_submissions_form_template_versions_template_version_id FOREIGN KEY (template_version_id) REFERENCES document.form_template_versions (id) ON DELETE RESTRICT
);


CREATE UNIQUE INDEX ix_approved_qrs_application_id ON document.approved_qrs (application_id);


CREATE INDEX ix_approved_qrs_submission_id ON document.approved_qrs (submission_id);


CREATE UNIQUE INDEX ix_form_template_versions_template_id_version_number ON document.form_template_versions (template_id, version_number);


CREATE UNIQUE INDEX ix_form_templates_code ON document.form_templates (code);


CREATE INDEX ix_generated_documents_submission_id ON document.generated_documents (submission_id);


CREATE INDEX ix_user_submissions_applicant_id ON document.user_submissions (applicant_id);


CREATE INDEX ix_user_submissions_applicant_status ON document.user_submissions (applicant_id, status);


CREATE UNIQUE INDEX ix_user_submissions_application_id_checklist_id ON document.user_submissions (application_id, checklist_id) WHERE application_id IS NOT NULL AND is_deleted = false;


CREATE INDEX ix_user_submissions_template_version_id ON document.user_submissions (template_version_id);


