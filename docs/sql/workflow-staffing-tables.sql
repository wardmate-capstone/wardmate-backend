-- Run in the Workflow database (public schema; shared PostgreSQL with DocumentForm) before or during deployment.
-- Idempotent script for 20261029161554_AddStaffingTablesOnly (STAFFING-01).
-- Requires 20261029010912_CommentWithdrawal (workflow-comment-withdrawal.sql) first.
-- Constraint and index names match the EF migration so later migrations can find them.
START TRANSACTION;

DO $EF$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL OR to_regclass('public.applications') IS NULL THEN
        RAISE EXCEPTION 'Workflow migration history or applications table missing. Select the correct database.';
    END IF;
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261029010912_CommentWithdrawal') THEN
        RAISE EXCEPTION 'Apply workflow-comment-withdrawal.sql (20261029010912_CommentWithdrawal) first.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261029161554_AddStaffingTablesOnly') THEN
        CREATE TABLE application_consultations (
            id uuid NOT NULL,
            application_id uuid NOT NULL,
            specialist_id uuid NOT NULL,
            requested_by uuid NOT NULL,
            application_version integer NOT NULL,
            assignment_version bigint NOT NULL,
            question character varying(4000) NOT NULL,
            answer character varying(4000),
            status character varying(20) NOT NULL,
            version integer NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            answered_at_utc timestamp with time zone,
            closed_at_utc timestamp with time zone,
            closed_by uuid,
            close_reason character varying(4000),
            CONSTRAINT pk_application_consultations PRIMARY KEY (id),
            CONSTRAINT ck_consultation_status CHECK (status IN ('OPEN','ANSWERED','CLOSED')),
            CONSTRAINT fk_application_consultations_applications_application_id FOREIGN KEY (application_id) REFERENCES applications (id) ON DELETE RESTRICT
        );

        CREATE TABLE officer_leaves (
            id uuid NOT NULL,
            officer_id uuid NOT NULL,
            ward_code character varying(50) NOT NULL,
            starts_at_utc timestamp with time zone NOT NULL,
            ends_at_utc timestamp with time zone NOT NULL,
            reason character varying(2000) NOT NULL,
            status character varying(20) NOT NULL,
            version integer NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            decided_by uuid,
            decided_at_utc timestamp with time zone,
            decision_reason character varying(2000),
            CONSTRAINT pk_officer_leaves PRIMARY KEY (id),
            CONSTRAINT ck_leave_range CHECK (ends_at_utc > starts_at_utc),
            CONSTRAINT ck_leave_status CHECK (status IN ('PENDING','APPROVED','REJECTED','CANCELLED'))
        );

        CREATE TABLE officer_shifts (
            id uuid NOT NULL,
            officer_id uuid NOT NULL,
            ward_code character varying(50) NOT NULL,
            starts_at_utc timestamp with time zone NOT NULL,
            ends_at_utc timestamp with time zone NOT NULL,
            created_by uuid NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            cancelled_by uuid,
            cancelled_at_utc timestamp with time zone,
            cancellation_reason character varying(2000),
            CONSTRAINT pk_officer_shifts PRIMARY KEY (id),
            CONSTRAINT ck_shift_range CHECK (ends_at_utc > starts_at_utc)
        );

        CREATE TABLE officer_leave_decisions (
            id uuid NOT NULL,
            leave_id uuid NOT NULL,
            actor_id uuid NOT NULL,
            version integer NOT NULL,
            from_status character varying(20) NOT NULL,
            to_status character varying(20) NOT NULL,
            reason character varying(2000) NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            CONSTRAINT pk_officer_leave_decisions PRIMARY KEY (id),
            CONSTRAINT fk_officer_leave_decisions_officer_leaves_leave_id FOREIGN KEY (leave_id) REFERENCES officer_leaves (id) ON DELETE RESTRICT
        );

        CREATE UNIQUE INDEX ix_application_consultations_application_id_specialist_id
            ON application_consultations (application_id, specialist_id) WHERE status <> 'CLOSED';
        CREATE UNIQUE INDEX ix_officer_leave_decisions_leave_id_version ON officer_leave_decisions (leave_id, version);
        CREATE INDEX ix_officer_leaves_ward_code_officer_id_starts_at_utc ON officer_leaves (ward_code, officer_id, starts_at_utc);
        CREATE INDEX ix_officer_shifts_ward_code_officer_id_starts_at_utc ON officer_shifts (ward_code, officer_id, starts_at_utc);

        INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
        VALUES ('20261029161554_AddStaffingTablesOnly', '8.0.11');
    END IF;
END $EF$;

COMMIT;

-- If any of the four tables already exists without a history row (created by hand earlier), the script stops
-- with "relation already exists" and changes nothing: compare that table with the migration before continuing.
