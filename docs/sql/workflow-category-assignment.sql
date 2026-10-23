-- Run in the Workflow database (public schema) before or during deployment.
-- Idempotent script for 20261023173500_CategoryAssignment
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261023173500_CategoryAssignment') THEN
        ALTER TABLE applications ADD COLUMN IF NOT EXISTS assigned_at timestamp with time zone;
        ALTER TABLE applications ADD COLUMN IF NOT EXISTS assigned_officer_name character varying(255);
        ALTER TABLE applications ADD COLUMN IF NOT EXISTS assignment_version bigint NOT NULL DEFAULT 0;
        ALTER TABLE applications ADD COLUMN IF NOT EXISTS category_id integer;
        ALTER TABLE applications ADD COLUMN IF NOT EXISTS category_name character varying(255);

        CREATE TABLE IF NOT EXISTS application_assignment_history (
            id uuid NOT NULL,
            application_id uuid NOT NULL,
            action character varying(20) NOT NULL,
            from_officer_id uuid,
            to_officer_id uuid,
            actor_id uuid NOT NULL,
            reason character varying(500),
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_application_assignment_history PRIMARY KEY (id),
            CONSTRAINT fk_application_assignment_history_applications_application_id FOREIGN KEY (application_id) REFERENCES applications (id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_applications_ward_code_category_id_status ON applications (ward_code, category_id, status);
        CREATE INDEX IF NOT EXISTS ix_application_assignment_history_application_id_created_at ON application_assignment_history (application_id, created_at);

        INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
        VALUES ('20261023173500_CategoryAssignment', '8.0.11');
    END IF;
END $EF$;

COMMIT;
