START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE SCHEMA IF NOT EXISTS audit_transport;
    CREATE TABLE IF NOT EXISTS audit_transport.outbox (
        id uuid PRIMARY KEY, source varchar(40) NOT NULL, payload jsonb NOT NULL,
        occurred_at timestamptz NOT NULL, delivered_at timestamptz,
        attempts integer NOT NULL DEFAULT 0, next_attempt_at timestamptz NOT NULL DEFAULT now(), last_error varchar(80));
    CREATE INDEX IF NOT EXISTS ix_audit_outbox_pending ON audit_transport.outbox(next_attempt_at, occurred_at) WHERE delivered_at IS NULL;

    CREATE OR REPLACE FUNCTION audit_transport.guard_outbox() RETURNS trigger LANGUAGE plpgsql AS $guard$
    BEGIN
      IF TG_OP='TRUNCATE' THEN RAISE EXCEPTION 'Audit evidence cannot be truncated'; END IF;
      IF TG_OP='DELETE' THEN
        IF OLD.delivered_at IS NULL THEN RAISE EXCEPTION 'Undelivered audit evidence cannot be deleted'; END IF;
        RETURN OLD;
      END IF;
      IF OLD.id IS DISTINCT FROM NEW.id OR OLD.source IS DISTINCT FROM NEW.source OR
         OLD.payload IS DISTINCT FROM NEW.payload OR OLD.occurred_at IS DISTINCT FROM NEW.occurred_at THEN
        RAISE EXCEPTION 'Audit evidence is immutable';
      END IF;
      RETURN NEW;
    END;
    $guard$;
    DO $install$
    BEGIN
      IF NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgrelid='audit_transport.outbox'::regclass AND tgname='audit_outbox_guard') THEN
        CREATE TRIGGER audit_outbox_guard BEFORE UPDATE OR DELETE ON audit_transport.outbox FOR EACH ROW EXECUTE FUNCTION audit_transport.guard_outbox();
        CREATE TRIGGER audit_outbox_no_truncate BEFORE TRUNCATE ON audit_transport.outbox FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.guard_outbox();
      END IF;
    END;
    $install$;

    CREATE OR REPLACE FUNCTION audit_transport.reject_truncate() RETURNS trigger LANGUAGE plpgsql AS $guard$
    BEGIN RAISE EXCEPTION 'Audited business tables must use DELETE instead of TRUNCATE'; END;
    $guard$;

    CREATE OR REPLACE FUNCTION audit_transport.capture() RETURNS trigger LANGUAGE plpgsql AS $audit$
    DECLARE
      old_row jsonb := CASE WHEN TG_OP='INSERT' THEN '{}'::jsonb ELSE to_jsonb(OLD) END;
      new_row jsonb := CASE WHEN TG_OP='DELETE' THEN '{}'::jsonb ELSE to_jsonb(NEW) END;
      before_data jsonb := '{}'::jsonb; after_data jsonb := '{}'::jsonb; identity_data jsonb := '{}'::jsonb;
      field text; event_id uuid := gen_random_uuid(); happened timestamptz := clock_timestamp();
      safe boolean; keys text[] := string_to_array(TG_ARGV[1], ',');
    BEGIN
      IF TG_OP='UPDATE' AND old_row=new_row THEN RETURN NEW; END IF;
      FOREACH field IN ARRAY keys LOOP
        identity_data := identity_data || jsonb_build_object(field, COALESCE(new_row->field,old_row->field));
      END LOOP;
      FOR field IN SELECT k FROM (SELECT jsonb_object_keys(old_row) k UNION SELECT jsonb_object_keys(new_row) k) fields LOOP
        IF TG_OP <> 'UPDATE' OR old_row->field IS DISTINCT FROM new_row->field THEN
          -- Default deny values: filenames, URLs, prompts, text, hashes, tokens, profiles and arbitrary JSON never leave source.
          safe := lower(replace(field,'_','')) = ANY(ARRAY[
            'id','userid','applicationid','procedureid','templateid','checklistid','submissionid','officerid',
            'assignedofficerid','actorid','roleid','permissionid','categoryid','wardid','status','state',
            'isactive','isdeleted','isread','isrequired','version','versionnumber','assignmentversion','rating']);
          IF old_row ? field THEN before_data := before_data || jsonb_build_object(field, CASE WHEN safe THEN old_row->field ELSE '"[REDACTED]"'::jsonb END); END IF;
          IF new_row ? field THEN after_data := after_data || jsonb_build_object(field, CASE WHEN safe THEN new_row->field ELSE '"[REDACTED]"'::jsonb END); END IF;
        END IF;
      END LOOP;
      INSERT INTO audit_transport.outbox(id,source,payload,occurred_at) VALUES(event_id,TG_ARGV[0],
        jsonb_build_object('eventId',event_id,'source',TG_ARGV[0],'schema',TG_TABLE_SCHEMA,'entity',TG_TABLE_NAME,
          'entityKey',identity_data,'action',TG_OP,'before',before_data,'after',after_data,
          'actorId',nullif(current_setting('wardmate.audit_actor',true),''),
          'clientIp',nullif(current_setting('wardmate.audit_ip',true),''),
          'traceId',nullif(current_setting('wardmate.audit_trace',true),''),'occurredAtUtc',happened),happened);
      RETURN CASE WHEN TG_OP='DELETE' THEN OLD ELSE NEW END;
    END;
    $audit$;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."applications" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."applications" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."application_checklists" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."application_checklists" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."application_comments" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."application_comments" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."application_consultations" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."application_consultations" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."application_assignment_history" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."application_assignment_history" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."application_status_history" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."application_status_history" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."application_versions" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."application_versions" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."officer_shifts" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."officer_shifts" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."officer_leaves" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."officer_leaves" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."officer_leave_decisions" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."officer_leave_decisions" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON "public"."calendar_closures" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('workflow', 'id');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON "public"."calendar_closures" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261102100002_TransactionalAuditSourceWorkflowDbContext') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261102100002_TransactionalAuditSourceWorkflowDbContext', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105055200_OfficerLeaveWordDrafts') THEN
    CREATE TABLE officer_leave_document_revisions (
        id uuid NOT NULL,
        draft_id uuid NOT NULL,
        metadata_version integer NOT NULL,
        sha256 text NOT NULL,
        size_bytes integer NOT NULL,
        saved_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_officer_leave_document_revisions PRIMARY KEY (id),
        CONSTRAINT ak_officer_leave_document_revisions_draft_id_id UNIQUE (draft_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105055200_OfficerLeaveWordDrafts') THEN
    CREATE TABLE officer_leave_drafts (
        id uuid NOT NULL,
        officer_id uuid NOT NULL,
        ward_code text NOT NULL,
        starts_at_utc timestamp with time zone NOT NULL,
        ends_at_utc timestamp with time zone NOT NULL,
        reason character varying(2000) NOT NULL,
        template_version_id uuid NOT NULL,
        template_hash text NOT NULL,
        version integer NOT NULL,
        metadata_version integer NOT NULL,
        creation_key uuid NOT NULL,
        creation_hash text NOT NULL,
        current_revision_id uuid,
        submitted_leave_id uuid,
        submission_key uuid,
        submission_hash text,
        updated_at_utc timestamp with time zone NOT NULL,
        deleted_at_utc timestamp with time zone,
        CONSTRAINT pk_officer_leave_drafts PRIMARY KEY (id),
        CONSTRAINT fk_officer_leave_drafts_leaves_submitted_leave_id FOREIGN KEY (submitted_leave_id) REFERENCES officer_leaves (id) ON DELETE RESTRICT,
        CONSTRAINT fk_officer_leave_drafts_officer_leave_document_revisions_id_cu FOREIGN KEY (id, current_revision_id) REFERENCES officer_leave_document_revisions (draft_id, id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105055200_OfficerLeaveWordDrafts') THEN
    CREATE INDEX ix_officer_leave_drafts_id_current_revision_id ON officer_leave_drafts (id, current_revision_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105055200_OfficerLeaveWordDrafts') THEN
    CREATE UNIQUE INDEX ix_officer_leave_drafts_officer_id_creation_key ON officer_leave_drafts (officer_id, creation_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105055200_OfficerLeaveWordDrafts') THEN
    CREATE UNIQUE INDEX ix_officer_leave_drafts_officer_id_submission_key ON officer_leave_drafts (officer_id, submission_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105055200_OfficerLeaveWordDrafts') THEN
    CREATE UNIQUE INDEX ix_officer_leave_drafts_submitted_leave_id ON officer_leave_drafts (submitted_leave_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105055200_OfficerLeaveWordDrafts') THEN
    CREATE INDEX ix_officer_leave_drafts_ward_code_officer_id_updated_at_utc ON officer_leave_drafts (ward_code, officer_id, updated_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105055200_OfficerLeaveWordDrafts') THEN
    ALTER TABLE officer_leave_document_revisions ADD CONSTRAINT fk_officer_leave_document_revisions_officer_leave_drafts_draft FOREIGN KEY (draft_id) REFERENCES officer_leave_drafts (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105055200_OfficerLeaveWordDrafts') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261105055200_OfficerLeaveWordDrafts', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105060056_MonthlyStaffSchedules') THEN
    ALTER TABLE officer_shifts ADD version integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105060056_MonthlyStaffSchedules') THEN
    CREATE TABLE officer_schedule_receipts (
        id uuid NOT NULL,
        ward_code text NOT NULL,
        month date NOT NULL,
        officer_id uuid NOT NULL,
        fingerprint text NOT NULL,
        confirmed_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_officer_schedule_receipts PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105060056_MonthlyStaffSchedules') THEN
    CREATE TABLE officer_shift_change_requests (
        id uuid NOT NULL,
        shift_id uuid NOT NULL,
        officer_id uuid NOT NULL,
        ward_code text NOT NULL,
        original_start timestamp with time zone NOT NULL,
        original_end timestamp with time zone NOT NULL,
        original_version integer NOT NULL,
        proposed_start timestamp with time zone NOT NULL,
        proposed_end timestamp with time zone NOT NULL,
        reason text NOT NULL,
        status text NOT NULL,
        decided_by uuid,
        decided_at_utc timestamp with time zone,
        decision_reason text,
        CONSTRAINT pk_officer_shift_change_requests PRIMARY KEY (id),
        CONSTRAINT fk_officer_shift_change_requests_shifts_shift_id FOREIGN KEY (shift_id) REFERENCES officer_shifts (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105060056_MonthlyStaffSchedules') THEN
    CREATE TABLE ward_schedule_revisions (
        id uuid NOT NULL,
        ward_code text NOT NULL,
        month date NOT NULL,
        version integer NOT NULL,
        status text NOT NULL,
        base_fingerprint text NOT NULL,
        slots_json jsonb NOT NULL,
        actor_id uuid NOT NULL,
        reason text NOT NULL,
        published_at_utc timestamp with time zone,
        CONSTRAINT pk_ward_schedule_revisions PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105060056_MonthlyStaffSchedules') THEN
    CREATE TABLE ward_week_patterns (
        ward_code text NOT NULL,
        version integer NOT NULL,
        entries_json jsonb NOT NULL,
        CONSTRAINT pk_ward_week_patterns PRIMARY KEY (ward_code)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105060056_MonthlyStaffSchedules') THEN
    CREATE UNIQUE INDEX ix_officer_schedule_receipts_ward_code_month_officer_id_finger ON officer_schedule_receipts (ward_code, month, officer_id, fingerprint);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105060056_MonthlyStaffSchedules') THEN
    CREATE UNIQUE INDEX ix_officer_shift_change_requests_shift_id ON officer_shift_change_requests (shift_id) WHERE status = 'PENDING';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105060056_MonthlyStaffSchedules') THEN
    CREATE UNIQUE INDEX ix_ward_schedule_revisions_ward_code_month ON ward_schedule_revisions (ward_code, month) WHERE status = 'DRAFT';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105060056_MonthlyStaffSchedules') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261105060056_MonthlyStaffSchedules', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105153205_LeaveDraftAudit') THEN
    ALTER TABLE officer_leave_drafts ADD submitted_officer_name text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105153205_LeaveDraftAudit') THEN
    CREATE TABLE officer_leave_draft_audit (
        id uuid NOT NULL,
        draft_id uuid NOT NULL,
        actor_id uuid NOT NULL,
        ward_code text NOT NULL,
        action text NOT NULL,
        version integer NOT NULL,
        revision_id uuid,
        occurred_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_officer_leave_draft_audit PRIMARY KEY (id),
        CONSTRAINT fk_officer_leave_draft_audit_leave_drafts_draft_id FOREIGN KEY (draft_id) REFERENCES officer_leave_drafts (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105153205_LeaveDraftAudit') THEN
    CREATE UNIQUE INDEX ix_officer_leave_draft_audit_draft_id_version ON officer_leave_draft_audit (draft_id, version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105153205_LeaveDraftAudit') THEN
    INSERT INTO ward_schedule_revisions
        (id, ward_code, month, version, status, base_fingerprint, slots_json, actor_id, reason, published_at_utc)
    SELECT gen_random_uuid(), ward_code,
           date_trunc('month', starts_at_utc AT TIME ZONE 'Asia/Ho_Chi_Minh')::date,
           1, 'BASELINE', '', jsonb_agg(jsonb_build_object(
               'ShiftId', id, 'OfficerId', officer_id, 'StartsAt', starts_at_utc,
               'EndsAt', ends_at_utc, 'ShiftVersion', version) ORDER BY starts_at_utc, id),
           '00000000-0000-0000-0000-000000000000'::uuid, 'Migration ca có hiệu lực', now()
    FROM officer_shifts WHERE cancelled_at_utc IS NULL
    GROUP BY ward_code, date_trunc('month', starts_at_utc AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261105153205_LeaveDraftAudit') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261105153205_LeaveDraftAudit', '8.0.11');
    END IF;
END $EF$;
COMMIT;

