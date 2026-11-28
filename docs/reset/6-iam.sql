-- RESET-DATA-1128 — IAM: bỏ lĩnh vực đã gán cho cán bộ (lĩnh vực cũ bị xóa ở 1-catalog.sql, id mới đếm lại từ 1
-- nên giữ id cũ sẽ gán nhầm cán bộ vào lĩnh vực mới). GIỮ tài khoản, vai trò, phường, quyền.
-- Chạy SAU khi đã sao lưu DB IAM. Sau khi nhập lại lĩnh vực, Quản lý phường gán lại lĩnh vực cho cán bộ.
BEGIN;
UPDATE users SET assigned_categories = '{}' WHERE cardinality(assigned_categories) > 0;
COMMIT;
