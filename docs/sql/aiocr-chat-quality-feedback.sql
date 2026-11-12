START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112162313_ChatQualityFeedback') THEN
    ALTER TABLE chat_messages ADD feedback_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112162313_ChatQualityFeedback') THEN
    ALTER TABLE chat_messages ADD feedback_comment character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112162313_ChatQualityFeedback') THEN
    ALTER TABLE chat_messages ADD feedback_helpful boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112162313_ChatQualityFeedback') THEN
    ALTER TABLE chat_messages ADD feedback_reason character varying(40);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112162313_ChatQualityFeedback') THEN
    ALTER TABLE chat_messages ADD needs_clarification boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112162313_ChatQualityFeedback') THEN
    ALTER TABLE chat_messages ADD suggested_questions jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112162313_ChatQualityFeedback') THEN
    CREATE INDEX ix_chat_messages_feedback_helpful_feedback_at ON chat_messages (feedback_helpful, feedback_at) WHERE feedback_helpful IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112162313_ChatQualityFeedback') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261112162313_ChatQualityFeedback', '8.0.11');
    END IF;
END $EF$;
COMMIT;

