-- Migration: AddWardLeaveTemplates
-- Supports manager-uploaded leave templates per ward with active-state tracking.

CREATE TABLE IF NOT EXISTS ward_leave_templates (
    id UUID NOT NULL,
    ward_code VARCHAR(50) NOT NULL,
    sha256 VARCHAR(64) NOT NULL,
    size_bytes INT NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    uploaded_by UUID NOT NULL,
    uploaded_by_name VARCHAR(255) NOT NULL,
    uploaded_at_utc TIMESTAMPTZ NOT NULL,
    is_active BOOLEAN NOT NULL,
    CONSTRAINT pk_ward_leave_templates PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_ward_leave_templates_ward_code_is_active_uploaded_at_utc
    ON ward_leave_templates (ward_code, is_active, uploaded_at_utc);
