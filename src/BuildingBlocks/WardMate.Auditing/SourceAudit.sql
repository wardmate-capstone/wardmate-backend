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
