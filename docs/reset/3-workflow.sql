-- RESET-DATA-1128 — ApplicationWorkflow: xóa hồ sơ và mọi dữ liệu con, hàng đợi sự kiện; GIỮ lịch làm, ca, nghỉ phép, ngày lễ
-- Chạy SAU khi đã sao lưu DB (pg_dump) theo docs/reset/README.md. KHÔNG hoàn tác được.
-- Bảng hồ sơ có trigger kiểm toán (audit_transport.reject_truncate) chặn TRUNCATE, nên dùng DELETE
-- theo thứ tự bảng con trước, bảng cha sau; mỗi dòng xóa được ghi nhật ký kiểm toán.
-- Bảng chưa tồn tại trên DB thì bỏ qua (ghi NOTICE). Không dùng CASCADE: nếu bảng ngoài danh sách
-- còn khóa ngoại trỏ vào thì lệnh lỗi và cả giao dịch quay lại (không xóa gì).
-- 28/11: thêm staffing_assignment_history (khóa ngoại tới applications).
BEGIN;
DO $$
DECLARE
  wanted text[] := ARRAY['staffing_assignment_history','application_checklists','application_status_history','application_assignment_history','application_comments','application_consultations','application_versions','applications','workflow_outbox'];
  t text;
  n bigint;
BEGIN
  FOREACH t IN ARRAY wanted LOOP
    IF to_regclass(t) IS NULL THEN RAISE NOTICE 'Bỏ qua bảng không có: %', t;
    ELSE
      EXECUTE 'DELETE FROM ' || t;
      GET DIAGNOSTICS n = ROW_COUNT;
      RAISE NOTICE 'Đã xóa % dòng: %', n, t;
    END IF;
  END LOOP;
END $$;
COMMIT;
