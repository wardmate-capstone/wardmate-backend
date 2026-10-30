-- Comprehensive & Idempotent SQL script for WardMate AIOCR Database (Supabase PostgreSQL)
-- Sets up all tables: ChatSessions, KnowledgeBase, KnowledgeRetrieval, and AiRequestLogs.
-- Safe to run on empty databases or partially migrated databases.

START TRANSACTION;
SET LOCAL search_path TO public;

-- 1. __EFMigrationsHistory
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL CONSTRAINT pk_ef_migrations_history PRIMARY KEY,
    product_version character varying(32) NOT NULL
);

-- 2. chat_sessions
CREATE TABLE IF NOT EXISTS chat_sessions (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    ward_code character varying(50) NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT pk_chat_sessions PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS ix_chat_sessions_user_id_created_at ON chat_sessions (user_id, created_at);

-- 3. chat_messages
CREATE TABLE IF NOT EXISTS chat_messages (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    session_id uuid NOT NULL,
    role character varying(20) NOT NULL,
    content character varying(20000) NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    citations jsonb,
    CONSTRAINT pk_chat_messages PRIMARY KEY (id),
    CONSTRAINT ck_chat_message_role CHECK (role IN ('User','Assistant')),
    CONSTRAINT fk_chat_messages_chat_sessions_session_id FOREIGN KEY (session_id) REFERENCES chat_sessions (id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_chat_messages_session_id_created_at ON chat_messages (session_id, created_at);
ALTER TABLE chat_messages ADD COLUMN IF NOT EXISTS citations jsonb;

-- 4. knowledge_documents
CREATE TABLE IF NOT EXISTS knowledge_documents (
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
    embedded_at timestamp with time zone,
    embedding_attempts integer NOT NULL DEFAULT 0,
    embedding_error character varying(100),
    embedding_model character varying(120),
    next_embedding_at timestamp with time zone,
    CONSTRAINT pk_knowledge_documents PRIMARY KEY (id),
    CONSTRAINT ck_knowledge_documents_procedure CHECK (source_type <> 'PROCEDURE' OR procedure_id IS NOT NULL),
    CONSTRAINT ck_knowledge_documents_source_type CHECK (source_type IN ('PROCEDURE','MANUAL_TEXT','FAQ','PDF_FILE'))
);

DO $status_ck$
BEGIN
    ALTER TABLE knowledge_documents DROP CONSTRAINT IF EXISTS ck_knowledge_documents_status;
    ALTER TABLE knowledge_documents ADD CONSTRAINT ck_knowledge_documents_status CHECK (status IN ('CHUNKED','EMBEDDED','EMBEDDING_FAILED'));
EXCEPTION WHEN OTHERS THEN NULL;
END $status_ck$;

CREATE UNIQUE INDEX IF NOT EXISTS ix_knowledge_documents_procedure_id ON knowledge_documents (procedure_id) WHERE source_type = 'PROCEDURE';
CREATE INDEX IF NOT EXISTS ix_knowledge_documents_source_type_is_active ON knowledge_documents (source_type, is_active);
CREATE INDEX IF NOT EXISTS ix_knowledge_documents_updated_at ON knowledge_documents (updated_at);
CREATE INDEX IF NOT EXISTS ix_knowledge_documents_status_is_active ON knowledge_documents (status, is_active);

-- 5. knowledge_chunks
CREATE TABLE IF NOT EXISTS knowledge_chunks (
    id uuid NOT NULL,
    document_id uuid NOT NULL,
    chunk_index integer NOT NULL,
    heading character varying(500),
    content text NOT NULL,
    char_count integer NOT NULL,
    content_sha256 character varying(64) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    embedded_at timestamp with time zone,
    embedding real[],
    embedding_model character varying(120),
    CONSTRAINT pk_knowledge_chunks PRIMARY KEY (id),
    CONSTRAINT fk_knowledge_chunks_knowledge_documents_document_id FOREIGN KEY (document_id) REFERENCES knowledge_documents (id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_knowledge_chunks_document_id_chunk_index ON knowledge_chunks (document_id, chunk_index);

-- 6. ai_request_logs
CREATE TABLE IF NOT EXISTS ai_request_logs (
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
CREATE INDEX IF NOT EXISTS ix_ai_request_logs_created_at ON ai_request_logs (created_at);
CREATE INDEX IF NOT EXISTS ix_ai_request_logs_user_id_created_at ON ai_request_logs (user_id, created_at);

-- 7. Record migrations into __EFMigrationsHistory
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES 
    ('20261021080951_ChatSessions', '8.0.11'),
    ('20261024034956_KnowledgeBase', '8.0.11'),
    ('20261024035926_KnowledgeRetrieval', '8.0.11'),
    ('20261030083635_AiRequestLogs', '8.0.11')
ON CONFLICT (migration_id) DO NOTHING;

COMMIT;

