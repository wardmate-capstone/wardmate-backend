-- Run in the AIOCR Supabase/PostgreSQL database before deploying the new AIOCR code.
-- Idempotent: 20261024034956_KnowledgeBase. Auto-provisions ChatSessions baseline if empty.
START TRANSACTION;
SET LOCAL search_path TO public;

DO $preflight$
BEGIN
    -- Auto-provision __EFMigrationsHistory if database is brand new
    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
        migration_id character varying(150) NOT NULL CONSTRAINT pk_ef_migrations_history PRIMARY KEY,
        product_version character varying(32) NOT NULL
    );

    -- Auto-provision baseline 20261021080951_ChatSessions if missing
    IF to_regclass('public.chat_sessions') IS NULL THEN
        CREATE TABLE IF NOT EXISTS chat_sessions (
            id uuid NOT NULL DEFAULT gen_random_uuid(),
            user_id uuid NOT NULL,
            ward_code character varying(50) NOT NULL,
            created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT pk_chat_sessions PRIMARY KEY (id)
        );
        CREATE INDEX IF NOT EXISTS ix_chat_sessions_user_id_created_at ON chat_sessions (user_id, created_at);

        CREATE TABLE IF NOT EXISTS chat_messages (
            id uuid NOT NULL DEFAULT gen_random_uuid(),
            session_id uuid NOT NULL,
            role character varying(20) NOT NULL,
            content character varying(20000) NOT NULL,
            created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT pk_chat_messages PRIMARY KEY (id),
            CONSTRAINT ck_chat_message_role CHECK (role IN ('User','Assistant')),
            CONSTRAINT fk_chat_messages_chat_sessions_session_id FOREIGN KEY (session_id) REFERENCES chat_sessions (id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS ix_chat_messages_session_id_created_at ON chat_messages (session_id, created_at);

        INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
        VALUES ('20261021080951_ChatSessions', '8.0.11')
        ON CONFLICT (migration_id) DO NOTHING;
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
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261024034956_KnowledgeBase') INTO v_has_migration;
    ELSE
        v_is_snake := false;
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024034956_KnowledgeBase') INTO v_has_migration;
    END IF;

    IF NOT v_has_migration THEN
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
            CONSTRAINT pk_knowledge_documents PRIMARY KEY (id),
            CONSTRAINT ck_knowledge_documents_procedure CHECK (source_type <> 'PROCEDURE' OR procedure_id IS NOT NULL),
            CONSTRAINT ck_knowledge_documents_source_type CHECK (source_type IN ('PROCEDURE','MANUAL_TEXT','FAQ','PDF_FILE')),
            CONSTRAINT ck_knowledge_documents_status CHECK (status IN ('CHUNKED'))
        );

        CREATE TABLE IF NOT EXISTS knowledge_chunks (
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

        CREATE UNIQUE INDEX IF NOT EXISTS ix_knowledge_chunks_document_id_chunk_index ON knowledge_chunks (document_id, chunk_index);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_knowledge_documents_procedure_id ON knowledge_documents (procedure_id) WHERE source_type = 'PROCEDURE';
        CREATE INDEX IF NOT EXISTS ix_knowledge_documents_source_type_is_active ON knowledge_documents (source_type, is_active);
        CREATE INDEX IF NOT EXISTS ix_knowledge_documents_updated_at ON knowledge_documents (updated_at);

        IF v_is_snake THEN
            INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
            VALUES ('20261024034956_KnowledgeBase', '8.0.11');
        ELSE
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20261024034956_KnowledgeBase', '8.0.11');
        END IF;
    END IF;
END $EF$;

COMMIT;
