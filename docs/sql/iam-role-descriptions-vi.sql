START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261128030412_RoleDescriptionsVi') THEN
    UPDATE roles SET description = 'Công dân đã đăng ký tài khoản, tự tạo và theo dõi hồ sơ của mình' WHERE id = 1 AND (description IS NULL OR btrim(description) = '' OR description = 'Registered citizen');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261128030412_RoleDescriptionsVi') THEN
    UPDATE roles SET description = 'Cán bộ một cửa: kiểm tra giấy tờ và tiếp nhận hồ sơ tại quầy' WHERE id = 2 AND (description IS NULL OR btrim(description) = '' OR description = 'Front desk officer');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261128030412_RoleDescriptionsVi') THEN
    UPDATE roles SET description = 'Quản lý phường: giám sát hồ sơ, phân công và xếp ca cán bộ' WHERE id = 3 AND (description IS NULL OR btrim(description) = '' OR description = 'Manager');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261128030412_RoleDescriptionsVi') THEN
    UPDATE roles SET description = 'Quản lý thủ tục: soạn, xuất bản thủ tục và biểu mẫu' WHERE id = 4 AND (description IS NULL OR btrim(description) = '' OR description = 'Procedure catalog manager');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261128030412_RoleDescriptionsVi') THEN
    UPDATE roles SET description = 'Quản trị hệ thống: tài khoản, vai trò, phường và cấu hình' WHERE id = 5 AND (description IS NULL OR btrim(description) = '' OR description = 'IT administrator');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261128030412_RoleDescriptionsVi') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261128030412_RoleDescriptionsVi', '8.0.11');
    END IF;
END $EF$;
COMMIT;

