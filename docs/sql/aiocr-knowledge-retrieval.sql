-- Run in the AIOCR Supabase/PostgreSQL database after aiocr-knowledge-base.sql and before deploying the TASK-29 AIOCR code.
-- Idempotent: 20261024035926_KnowledgeRetrieval. Vectors are stored as real[]; pgvector is NOT required.
START TRANSACTION;
SET LOCAL search_path TO public;

DO $preflight$
DECLARE
    v_has_baseline boolean := false;
BEGIN
    IF to_regclass('public.knowledge_documents') IS NULL OR to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'knowledge_documents or migration history missing. Run aiocr-knowledge-base.sql in the AIOCR database first.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = '__EFMigrationsHistory' AND column_name = 'migration_id'
    ) THEN
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261024034956_KnowledgeBase') INTO v_has_baseline;
    ELSE
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024034956_KnowledgeBase') INTO v_has_baseline;
    END IF;

    IF NOT v_has_baseline THEN
        RAISE EXCEPTION 'Baseline 20261024034956_KnowledgeBase is required. Run aiocr-knowledge-base.sql first.';
    END IF;
END $preflight$;

DO $EF$
DECLARE
    v_has_migration boolean := false;
    v_is_snake boolean := true;
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = '__EFMigrationsHistory' AND column_name = 'migration_id'
    ) THEN
        v_is_snake := true;
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261024035926_KnowledgeRetrieval') INTO v_has_migration;
    ELSE
        v_is_snake := false;
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024035926_KnowledgeRetrieval') INTO v_has_migration;
    END IF;

    IF NOT v_has_migration THEN
        ALTER TABLE knowledge_documents DROP CONSTRAINT IF EXISTS ck_knowledge_documents_status;
        ALTER TABLE knowledge_documents ADD COLUMN IF NOT EXISTS embedded_at timestamp with time zone;
        ALTER TABLE knowledge_documents ADD COLUMN IF NOT EXISTS embedding_attempts integer NOT NULL DEFAULT 0;
        ALTER TABLE knowledge_documents ADD COLUMN IF NOT EXISTS embedding_error character varying(100);
        ALTER TABLE knowledge_documents ADD COLUMN IF NOT EXISTS embedding_model character varying(120);
        ALTER TABLE knowledge_documents ADD COLUMN IF NOT EXISTS next_embedding_at timestamp with time zone;

        ALTER TABLE knowledge_chunks ADD COLUMN IF NOT EXISTS embedded_at timestamp with time zone;
        ALTER TABLE knowledge_chunks ADD COLUMN IF NOT EXISTS embedding real[];
        ALTER TABLE knowledge_chunks ADD COLUMN IF NOT EXISTS embedding_model character varying(120);

        ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS citations jsonb;

        CREATE INDEX IF NOT EXISTS ix_knowledge_documents_status_is_active ON knowledge_documents (status, is_active);

        ALTER TABLE knowledge_documents ADD CONSTRAINT ck_knowledge_documents_status CHECK (status IN ('CHUNKED','EMBEDDED','EMBEDDING_FAILED'));

        IF v_is_snake THEN
            INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
            VALUES ('20261024035926_KnowledgeRetrieval', '8.0.11');
        ELSE
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20261024035926_KnowledgeRetrieval', '8.0.11');
        END IF;
    END IF;
END $EF$;

COMMIT;
