CREATE SEQUENCE application_code_sequence START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;


CREATE TABLE applications (
    id uuid NOT NULL,
    application_code character varying(50) NOT NULL,
    user_id uuid NOT NULL,
    procedure_id uuid NOT NULL,
    ward_code character varying(50),
    procedure_title character varying(500) NOT NULL,
    category_id integer,
    category_name character varying(255),
    assigned_officer_name character varying(255),
    assigned_at timestamp with time zone,
    assignment_version bigint NOT NULL DEFAULT 0,
    case_code character varying(100),
    status character varying(50) NOT NULL DEFAULT 'DRAFT',
    form_data jsonb NOT NULL,
    submitted_at timestamp with time zone,
    first_submitted_at_utc timestamp with time zone,
    due_at_utc timestamp with time zone,
    deadline_status character varying(20) NOT NULL DEFAULT 'UNKNOWN',
    deadline_policy_code character varying(100),
    deadline_policy_version character varying(100),
    deadline_calendar_code character varying(100),
    deadline_calendar_version character varying(100),
    deadline_policy_snapshot jsonb,
    deadline_paused_at_utc timestamp with time zone,
    deadline_history jsonb NOT NULL DEFAULT '[]',
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    assigned_officer_id uuid,
    resubmit_count integer NOT NULL DEFAULT 0,
    notes text,
    approved_at timestamp with time zone,
    documents_ready_at timestamp with time zone,
    receipt_number character varying(64),
    received_at timestamp with time zone,
    appointment_date timestamp with time zone,
    CONSTRAINT pk_applications PRIMARY KEY (id),
    CONSTRAINT ck_applications_deadline CHECK ((deadline_status = 'KNOWN' AND due_at_utc IS NOT NULL AND first_submitted_at_utc IS NOT NULL AND deadline_policy_snapshot IS NOT NULL AND deadline_policy_code IS NOT NULL AND deadline_policy_version IS NOT NULL AND deadline_calendar_code IS NOT NULL AND deadline_calendar_version IS NOT NULL) OR (deadline_status IN ('UNKNOWN','NOT_APPLICABLE') AND due_at_utc IS NULL AND deadline_paused_at_utc IS NULL)),
    CONSTRAINT ck_applications_status CHECK (status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_REVISION','DOCUMENTS_READY','OFFICIALLY_RECEIVED','APPROVED','CANCELLED','REJECTED'))
);


CREATE TABLE workflow_outbox (
    id uuid NOT NULL,
    payload jsonb NOT NULL,
    created_at timestamp with time zone NOT NULL,
    next_attempt_at timestamp with time zone NOT NULL,
    published_at timestamp with time zone,
    attempts integer NOT NULL,
    last_error character varying(100),
    CONSTRAINT pk_workflow_outbox PRIMARY KEY (id)
);


CREATE TABLE application_assignment_history (
    id uuid NOT NULL,
    application_id uuid NOT NULL,
    action character varying(20) NOT NULL,
    from_officer_id uuid,
    to_officer_id uuid,
    actor_id uuid NOT NULL,
    reason character varying(500),
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_application_assignment_history PRIMARY KEY (id),
    CONSTRAINT fk_application_assignment_history_applications_application_id FOREIGN KEY (application_id) REFERENCES applications (id) ON DELETE CASCADE
);


CREATE TABLE application_checklists (
    id uuid NOT NULL,
    sort_order integer NOT NULL,
    case_code text,
    submission_type character varying(30),
    document_copy_type character varying(30),
    quantity integer NOT NULL DEFAULT 1,
    condition_note text,
    form_template_id uuid,
    application_id uuid NOT NULL,
    code character varying(255) NOT NULL,
    title character varying(255) NOT NULL,
    is_required boolean NOT NULL,
    status character varying(30) NOT NULL DEFAULT 'PENDING',
    file_url character varying(500),
    note character varying(4000),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_application_checklists PRIMARY KEY (id),
    CONSTRAINT ck_checklists_status CHECK (status IN ('PENDING','COMPLETED','REJECTED')),
    CONSTRAINT fk_application_checklists_applications_application_id FOREIGN KEY (application_id) REFERENCES applications (id) ON DELETE CASCADE
);


CREATE TABLE application_status_history (
    id uuid NOT NULL,
    application_id uuid NOT NULL,
    from_status character varying(50),
    to_status character varying(50) NOT NULL,
    changed_by uuid NOT NULL,
    reason text,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT pk_application_status_history PRIMARY KEY (id),
    CONSTRAINT fk_application_status_history_applications_application_id FOREIGN KEY (application_id) REFERENCES applications (id) ON DELETE CASCADE
);


CREATE TABLE application_versions (
    id uuid NOT NULL,
    application_id uuid NOT NULL,
    version_number integer NOT NULL,
    submitted_by uuid NOT NULL,
    submitted_at timestamp with time zone NOT NULL,
    snapshot_data jsonb NOT NULL,
    CONSTRAINT pk_application_versions PRIMARY KEY (id),
    CONSTRAINT ak_application_versions_application_id_id UNIQUE (application_id, id),
    CONSTRAINT ck_application_versions_number CHECK (version_number > 0),
    CONSTRAINT fk_application_versions_applications_application_id FOREIGN KEY (application_id) REFERENCES applications (id) ON DELETE CASCADE
);


CREATE TABLE application_comments (
    id uuid NOT NULL,
    application_id uuid NOT NULL,
    application_version_id uuid NOT NULL,
    officer_id uuid NOT NULL,
    target_type character varying(30) NOT NULL,
    target_id character varying(100) NOT NULL,
    field_label character varying(255) NOT NULL,
    comment_text text NOT NULL,
    status character varying(20) NOT NULL DEFAULT 'OPEN',
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_application_comments PRIMARY KEY (id),
    CONSTRAINT ck_application_comments_status CHECK (status IN ('OPEN','RESOLVED')),
    CONSTRAINT ck_application_comments_target CHECK (target_type IN ('FORM_FIELD','CHECKLIST_ITEM')),
    CONSTRAINT fk_application_comments_application_versions_application_id_ap FOREIGN KEY (application_id, application_version_id) REFERENCES application_versions (application_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_application_comments_applications_application_id FOREIGN KEY (application_id) REFERENCES applications (id) ON DELETE CASCADE
);


CREATE INDEX ix_application_assignment_history_application_id_created_at ON application_assignment_history (application_id, created_at);


CREATE UNIQUE INDEX ix_application_checklists_application_id_code ON application_checklists (application_id, code);


CREATE INDEX ix_application_comments_application_id_application_version_id_ ON application_comments (application_id, application_version_id, status);


CREATE INDEX ix_application_status_history_application_id_created_at ON application_status_history (application_id, created_at);


CREATE UNIQUE INDEX ix_application_versions_application_id_version_number ON application_versions (application_id, version_number);


CREATE UNIQUE INDEX ix_applications_application_code ON applications (application_code);


CREATE INDEX ix_applications_assigned_officer_id ON applications (assigned_officer_id);


CREATE UNIQUE INDEX ix_applications_receipt_number ON applications (receipt_number);


CREATE INDEX ix_applications_status_submitted_at_id ON applications (status, submitted_at, id);


CREATE INDEX ix_applications_user_id_created_at ON applications (user_id, created_at);


CREATE INDEX ix_applications_ward_code_category_id_status ON applications (ward_code, category_id, status);


CREATE INDEX ix_applications_ward_code_deadline_status_due_at_utc ON applications (ward_code, deadline_status, due_at_utc);


CREATE INDEX ix_applications_ward_code_status_submitted_at ON applications (ward_code, status, submitted_at);


CREATE INDEX ix_workflow_outbox_published_at_next_attempt_at ON workflow_outbox (published_at, next_attempt_at);


