-- Run in the AIOCR database after aiocr-knowledge-base.sql and before deploying the TASK-29 AIOCR code.
-- Idempotent: 20261024035926_KnowledgeRetrieval. Vectors are stored as real[]; pgvector is NOT required.
START TRANSACTION;
SET LOCAL search_path TO public;
DO $preflight$
BEGIN
    IF to_regclass('public.knowledge_documents') IS NULL OR to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'knowledge_documents or migration history missing. Run aiocr-knowledge-base.sql in the AIOCR database first.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE migration_id = '20261024034956_KnowledgeBase') THEN
        RAISE EXCEPTION 'Baseline 20261024034956_KnowledgeBase is required.';
    END IF;
END $preflight$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE knowledge_documents DROP CONSTRAINT ck_knowledge_documents_status;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE knowledge_documents ADD embedded_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE knowledge_documents ADD embedding_attempts integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE knowledge_documents ADD embedding_error character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE knowledge_documents ADD embedding_model character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE knowledge_documents ADD next_embedding_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE knowledge_chunks ADD embedded_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE knowledge_chunks ADD embedding real[];
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE knowledge_chunks ADD embedding_model character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE chat_messages ADD citations jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    CREATE INDEX ix_knowledge_documents_status_is_active ON knowledge_documents (status, is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    ALTER TABLE knowledge_documents ADD CONSTRAINT ck_knowledge_documents_status CHECK (status IN ('CHUNKED','EMBEDDED','EMBEDDING_FAILED'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024035926_KnowledgeRetrieval') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261024035926_KnowledgeRetrieval', '8.0.11');
    END IF;
END $EF$;
COMMIT;

