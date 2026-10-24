CREATE TABLE chat_sessions (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    user_id uuid NOT NULL,
    ward_code character varying(50) NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT pk_chat_sessions PRIMARY KEY (id)
);


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
    embedding_model character varying(120),
    embedding_attempts integer NOT NULL,
    next_embedding_at timestamp with time zone,
    embedding_error character varying(100),
    embedded_at timestamp with time zone,
    revision uuid NOT NULL,
    CONSTRAINT pk_knowledge_documents PRIMARY KEY (id),
    CONSTRAINT ck_knowledge_documents_procedure CHECK (source_type <> 'PROCEDURE' OR procedure_id IS NOT NULL),
    CONSTRAINT ck_knowledge_documents_source_type CHECK (source_type IN ('PROCEDURE','MANUAL_TEXT','FAQ','PDF_FILE')),
    CONSTRAINT ck_knowledge_documents_status CHECK (status IN ('CHUNKED','EMBEDDED','EMBEDDING_FAILED'))
);


CREATE TABLE chat_messages (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    session_id uuid NOT NULL,
    role character varying(20) NOT NULL,
    content character varying(20000) NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    citations jsonb,
    CONSTRAINT pk_chat_messages PRIMARY KEY (id),
    CONSTRAINT ck_chat_message_role CHECK (role IN ('User','Assistant')),
    CONSTRAINT fk_chat_messages_chat_sessions_session_id FOREIGN KEY (session_id) REFERENCES chat_sessions (id) ON DELETE CASCADE
);


CREATE TABLE knowledge_chunks (
    id uuid NOT NULL,
    document_id uuid NOT NULL,
    chunk_index integer NOT NULL,
    heading character varying(500),
    content text NOT NULL,
    char_count integer NOT NULL,
    content_sha256 character varying(64) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    embedding real[],
    embedding_model character varying(120),
    embedded_at timestamp with time zone,
    CONSTRAINT pk_knowledge_chunks PRIMARY KEY (id),
    CONSTRAINT fk_knowledge_chunks_knowledge_documents_document_id FOREIGN KEY (document_id) REFERENCES knowledge_documents (id) ON DELETE CASCADE
);


CREATE INDEX ix_chat_messages_session_id_created_at ON chat_messages (session_id, created_at);


CREATE INDEX ix_chat_sessions_user_id_created_at ON chat_sessions (user_id, created_at);


CREATE UNIQUE INDEX ix_knowledge_chunks_document_id_chunk_index ON knowledge_chunks (document_id, chunk_index);


CREATE UNIQUE INDEX ix_knowledge_documents_procedure_id ON knowledge_documents (procedure_id) WHERE source_type = 'PROCEDURE';


CREATE INDEX ix_knowledge_documents_source_type_is_active ON knowledge_documents (source_type, is_active);


CREATE INDEX ix_knowledge_documents_status_is_active ON knowledge_documents (status, is_active);


CREATE INDEX ix_knowledge_documents_updated_at ON knowledge_documents (updated_at);


