-- RESET-DATA-1128 — ApplicationWorkflow: xóa hồ sơ và mọi dữ liệu con, hàng đợi sự kiện; GIỮ lịch làm, ca, nghỉ phép, ngày lễ
-- Chạy SAU khi đã sao lưu DB (pg_dump) theo docs/reset/README.md. KHÔNG hoàn tác được.
-- Chỉ xóa dữ liệu (TRUNCATE ... RESTART IDENTITY), giữ cấu trúc bảng và __EFMigrationsHistory.
-- Bảng chưa tồn tại trên DB thì bỏ qua (ghi NOTICE). Không dùng CASCADE: nếu bảng ngoài danh sách
-- còn khóa ngoại trỏ vào thì lệnh lỗi và cả giao dịch quay lại (không xóa gì).
BEGIN;
DO $$
DECLARE
  wanted text[] := ARRAY['applications','application_checklists','application_status_history','application_assignment_history','application_comments','application_consultations','application_versions','workflow_outbox'];
  existing text[];
  t text;
BEGIN
  FOREACH t IN ARRAY wanted LOOP
    IF to_regclass(t) IS NULL THEN RAISE NOTICE 'Bỏ qua bảng không có: %', t;
    ELSE existing := array_append(existing, t); END IF;
  END LOOP;
  IF existing IS NOT NULL THEN
    EXECUTE 'TRUNCATE TABLE ' || array_to_string(existing, ', ') || ' RESTART IDENTITY';
    RAISE NOTICE 'Đã xóa dữ liệu: %', array_to_string(existing, ', ');
  END IF;
END $$;
COMMIT;
