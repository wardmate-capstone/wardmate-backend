-- Run in the AIOCR Supabase/PostgreSQL database (wardmate-db on Azure) before deploying the AI-USAGE-01 AIOCR code.
-- Idempotent: 20261030083635_AiRequestLogs. Requires 20261024035926_KnowledgeRetrieval (aiocr-knowledge-retrieval.sql).
-- Not needed when AIOCR runs with Database:AutoMigrate=true.
START TRANSACTION;
SET LOCAL search_path TO public;

DO $EF$
DECLARE
    v_is_snake boolean;
    v_has_baseline boolean;
    v_has_migration boolean;
BEGIN
    IF to_regclass('public.chat_sessions') IS NULL OR to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'chat_sessions or migration history missing. Select the AIOCR database.';
    END IF;
    v_is_snake := EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory' AND column_name = 'migration_id');
    IF v_is_snake THEN
        v_has_baseline := EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261024035926_KnowledgeRetrieval');
        v_has_migration := EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261030083635_AiRequestLogs');
    ELSE
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = $1)' INTO v_has_baseline USING '20261024035926_KnowledgeRetrieval';
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = $1)' INTO v_has_migration USING '20261030083635_AiRequestLogs';
    END IF;
    IF NOT v_has_baseline THEN
        RAISE EXCEPTION 'Baseline 20261024035926_KnowledgeRetrieval is required. Run aiocr-knowledge-retrieval.sql first.';
    END IF;

    IF NOT v_has_migration THEN
        CREATE TABLE ai_request_logs (
            id uuid NOT NULL,
            created_at timestamp with time zone NOT NULL,
            user_id uuid,
            operation character varying(40) NOT NULL,
            model character varying(120) NOT NULL,
            outcome character varying(20) NOT NULL,
            error_code character varying(100),
            prompt_tokens integer,
            output_tokens integer,
            total_tokens integer,
            duration_ms integer NOT NULL,
            item_count integer NOT NULL,
            CONSTRAINT pk_ai_request_logs PRIMARY KEY (id),
            CONSTRAINT ck_ai_request_logs_counts CHECK (duration_ms >= 0 AND item_count >= 1),
            CONSTRAINT ck_ai_request_logs_outcome CHECK (outcome IN ('SUCCESS','FAILED','REJECTED'))
        );
        CREATE INDEX ix_ai_request_logs_created_at ON ai_request_logs (created_at);
        CREATE INDEX ix_ai_request_logs_user_id_created_at ON ai_request_logs (user_id, created_at);

        IF v_is_snake THEN
            INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) VALUES ('20261030083635_AiRequestLogs', '8.0.11');
        ELSE
            EXECUTE 'INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ($1, $2)' USING '20261030083635_AiRequestLogs', '8.0.11';
        END IF;
    END IF;
END $EF$;

COMMIT;

-- Supabase: the new table is in the public schema. With the Data API disabled it is not exposed; if the Data API is ever
-- re-enabled, enable RLS on ai_request_logs (no policies) like the other AIOCR tables.
