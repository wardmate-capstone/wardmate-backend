-- Run in the AIOCR database (ConnectionStrings:AiOcrDatabase) before deploying the new AIOCR code.
-- Idempotent: 20261024034956_KnowledgeBase. No pgvector needed (embeddings come with TASK-29).
START TRANSACTION;
SET LOCAL search_path TO public;
DO $preflight$
BEGIN
    IF to_regclass('public.chat_sessions') IS NULL OR to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'chat_sessions or migration history missing. Select the AIOCR database.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE migration_id = '20261021080951_ChatSessions') THEN
        RAISE EXCEPTION 'Baseline 20261021080951_ChatSessions is required.';
    END IF;
END $preflight$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024034956_KnowledgeBase') THEN
    CREATE TABLE knowledge_documents (
        id uuid NOT NULL,
        title character varying(500) NOT NULL,
        source_type character varying(20) NOT NULL,
        procedure_id uuid,
        source_reference character varying(500),
        source_version character varying(100),
        content text NOT NULL,
        content_sha256 character varying(64) NOT NULL,
        status character varying(20) NOT NULL,
        is_active boolean NOT NULL,
        chunk_count integer NOT NULL,
        chunking_version character varying(50) NOT NULL,
        created_by uuid NOT NULL,
        updated_by uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        chunked_at timestamp with time zone,
        revision uuid NOT NULL,
        CONSTRAINT pk_knowledge_documents PRIMARY KEY (id),
        CONSTRAINT ck_knowledge_documents_procedure CHECK (source_type <> 'PROCEDURE' OR procedure_id IS NOT NULL),
        CONSTRAINT ck_knowledge_documents_source_type CHECK (source_type IN ('PROCEDURE','MANUAL_TEXT','FAQ','PDF_FILE')),
        CONSTRAINT ck_knowledge_documents_status CHECK (status IN ('CHUNKED'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024034956_KnowledgeBase') THEN
    CREATE TABLE knowledge_chunks (
        id uuid NOT NULL,
        document_id uuid NOT NULL,
        chunk_index integer NOT NULL,
        heading character varying(500),
        content text NOT NULL,
        char_count integer NOT NULL,
        content_sha256 character varying(64) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_knowledge_chunks PRIMARY KEY (id),
        CONSTRAINT fk_knowledge_chunks_knowledge_documents_document_id FOREIGN KEY (document_id) REFERENCES knowledge_documents (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024034956_KnowledgeBase') THEN
    CREATE UNIQUE INDEX ix_knowledge_chunks_document_id_chunk_index ON knowledge_chunks (document_id, chunk_index);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024034956_KnowledgeBase') THEN
    CREATE UNIQUE INDEX ix_knowledge_documents_procedure_id ON knowledge_documents (procedure_id) WHERE source_type = 'PROCEDURE';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024034956_KnowledgeBase') THEN
    CREATE INDEX ix_knowledge_documents_source_type_is_active ON knowledge_documents (source_type, is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024034956_KnowledgeBase') THEN
    CREATE INDEX ix_knowledge_documents_updated_at ON knowledge_documents (updated_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024034956_KnowledgeBase') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261024034956_KnowledgeBase', '8.0.11');
    END IF;
END $EF$;
COMMIT;

