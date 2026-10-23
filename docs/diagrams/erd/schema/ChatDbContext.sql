CREATE TABLE chat_sessions (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    user_id uuid NOT NULL,
    ward_code character varying(50) NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT pk_chat_sessions PRIMARY KEY (id)
);


CREATE TABLE chat_messages (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    session_id uuid NOT NULL,
    role character varying(20) NOT NULL,
    content character varying(20000) NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT pk_chat_messages PRIMARY KEY (id),
    CONSTRAINT ck_chat_message_role CHECK (role IN ('User','Assistant')),
    CONSTRAINT fk_chat_messages_chat_sessions_session_id FOREIGN KEY (session_id) REFERENCES chat_sessions (id) ON DELETE CASCADE
);


CREATE INDEX ix_chat_messages_session_id_created_at ON chat_messages (session_id, created_at);


CREATE INDEX ix_chat_sessions_user_id_created_at ON chat_sessions (user_id, created_at);


