START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112161559_UserAvatar') THEN
    ALTER TABLE users ADD avatar_updated_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112161559_UserAvatar') THEN
    CREATE TABLE user_avatars (
        user_id uuid NOT NULL,
        content bytea NOT NULL,
        content_type character varying(50) NOT NULL,
        sha256 character varying(64) NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_user_avatars PRIMARY KEY (user_id),
        CONSTRAINT ck_user_avatars_size CHECK (octet_length(content) <= 524288),
        CONSTRAINT fk_user_avatars_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261112161559_UserAvatar') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261112161559_UserAvatar', '8.0.11');
    END IF;
END $EF$;
COMMIT;

